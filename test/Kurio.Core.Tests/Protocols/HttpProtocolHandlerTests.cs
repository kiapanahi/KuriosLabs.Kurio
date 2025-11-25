namespace Kurio.Core.Tests.Protocols;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Kurio.Core.Models;
using Kurio.Core.Protocols;

public sealed class HttpProtocolHandlerTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<HttpProtocolHandler>> _loggerMock;
    private readonly HttpProtocolHandler _handler;

    public HttpProtocolHandlerTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<HttpProtocolHandler>>();
        _handler = new HttpProtocolHandler(_httpClientFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HttpProtocolHandler(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void SupportedSchemes_ContainsHttpAndHttps()
    {
        // Assert
        _handler.SupportedSchemes.Should().Contain(new[] { "http", "https" });
        _handler.SupportedSchemes.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("bytes, none")]
    public async Task SupportsRangeRequestsAsync_WithAcceptRangesHeader_ReturnsTrue(string acceptRanges)
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("Accept-Ranges", acceptRanges);
                return response;
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        // Act
        var result = await _handler.SupportsRangeRequestsAsync(url, options);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SupportsRangeRequestsAsync_WithAcceptRangesNone_ReturnsFalse()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("Accept-Ranges", "none");
                return response;
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        // Act
        var result = await _handler.SupportsRangeRequestsAsync(url, options);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SupportsRangeRequestsAsync_WithNoAcceptRangesHeader_ReturnsFalse()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        // Act
        var result = await _handler.SupportsRangeRequestsAsync(url, options);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetFileSizeAsync_ReturnsContentLength()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var options = CreateDefaultOptions();
        const long expectedSize = 1024 * 1024; // 1 MB

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
                response.Content.Headers.ContentLength = expectedSize;
                return response;
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        // Act
        var result = await _handler.GetFileSizeAsync(url, options);

        // Assert
        result.Should().Be(expectedSize);
    }

    [Fact]
    public async Task GetFileSizeAsync_WithNoContentLength_ReturnsMinusOne()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("test") // Use StringContent which may set ContentLength
                };
                // Explicitly set ContentLength to null
                response.Content.Headers.ContentLength = null;
                return response;
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        // Act
        var result = await _handler.GetFileSizeAsync(url, options);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public async Task DownloadRangeAsync_DownloadsDataSuccessfully()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var range = new ByteRange(0, 99);
        var options = CreateDefaultOptions();
        var testData = new byte[100];
        for (int i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.Headers.Range != null),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(testData)
                };
                return response;
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        using var destination = new MemoryStream();
        long reportedProgress = 0;
        var progress = new Progress<long>(p => reportedProgress = p);

        // Act
        await _handler.DownloadRangeAsync(url, range, destination, options, progress);

        // Assert
        destination.ToArray().Should().Equal(testData);
        reportedProgress.Should().Be(testData.Length);
    }

    [Fact]
    public async Task DownloadRangeAsync_WithNonPartialContentForRangeRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var range = new ByteRange(100, 199); // Non-zero start
        var options = CreateDefaultOptions();

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                // Server returns 200 OK instead of 206 Partial Content
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[100])
                };
                return response;
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        using var destination = new MemoryStream();

        // Act & Assert
        var act = async () => await _handler.DownloadRangeAsync(url, range, destination, options);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*did not honor range request*");
    }

    [Fact]
    public async Task GetMetadataAsync_ReturnsCompleteMetadata()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var options = CreateDefaultOptions();
        var expectedETag = "\"abc123\"";
        var expectedLastModified = DateTimeOffset.UtcNow;
        const long expectedSize = 2048;

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
                response.Content.Headers.ContentLength = expectedSize;
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
                response.Content.Headers.LastModified = expectedLastModified;
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(expectedETag);
                response.Headers.Add("Accept-Ranges", "bytes");
                response.Content.Headers.ContentDisposition = 
                    new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    {
                        FileName = "test-file.zip"
                    };
                return response;
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        // Act
        var result = await _handler.GetMetadataAsync(url, options);

        // Assert
        result.Should().NotBeNull();
        result.ContentLength.Should().Be(expectedSize);
        result.ContentType.Should().Be("application/zip");
        result.ETag.Should().Be(expectedETag);
        result.LastModified.Should().Be(expectedLastModified);
        result.SupportsRanges.Should().BeTrue();
        result.SuggestedFileName.Should().Be("test-file.zip");
    }

    [Fact]
    public async Task SupportsRangeRequestsAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        var act = async () => await _handler.SupportsRangeRequestsAsync(null!, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetFileSizeAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        var act = async () => await _handler.GetFileSizeAsync(null!, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadRangeAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var range = new ByteRange(0, 99);
        var options = CreateDefaultOptions();
        using var destination = new MemoryStream();

        // Act & Assert
        var act = async () => await _handler.DownloadRangeAsync(null!, range, destination, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadRangeAsync_WithNonWritableStream_ThrowsArgumentException()
    {
        // Arrange
        var url = new Uri("https://example.com/file.zip");
        var range = new ByteRange(0, 99);
        var options = CreateDefaultOptions();
        using var destination = new MemoryStream(new byte[100], writable: false);

        // Act & Assert
        var act = async () => await _handler.DownloadRangeAsync(url, range, destination, options);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must be writable*");
    }

    [Fact]
    public async Task GetMetadataAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        var act = async () => await _handler.GetMetadataAsync(null!, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static DownloadOptions CreateDefaultOptions()
    {
        return new DownloadOptions
        {
            DestinationDirectory = "/downloads",
            UserAgent = "Kurio/1.0",
            TimeoutSeconds = 30,
            Headers = new Dictionary<string, string>()
        };
    }
}
