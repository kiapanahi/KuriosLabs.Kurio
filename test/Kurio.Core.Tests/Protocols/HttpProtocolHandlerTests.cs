using System.Net;
using System.Net.Http.Headers;

using FluentAssertions;

using Kurio.Core.Models;
using Kurio.Core.Protocols;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;

namespace Kurio.Core.Tests.Protocols;

public sealed class HttpProtocolHandlerTests
{
    private readonly HttpProtocolHandler _handler;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<HttpProtocolHandler>> _loggerMock;

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
        Uri url = new("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK);
                response.Headers.Add("Accept-Ranges", acceptRanges);
                return response;
            });

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
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
        Uri url = new("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK);
                response.Headers.Add("Accept-Ranges", "none");
                return response;
            });

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
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
        Uri url = new("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
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
        Uri url = new("https://example.com/file.zip");
        var options = CreateDefaultOptions();
        const long expectedSize = 1024 * 1024; // 1 MB

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
                response.Content.Headers.ContentLength = expectedSize;
                return response;
            });

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
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
        Uri url = new("https://example.com/file.zip");
        var options = CreateDefaultOptions();

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK)
                {
                    Content = new StringContent("test") // Use StringContent which may set ContentLength
                };
                // Explicitly set ContentLength to null
                response.Content.Headers.ContentLength = null;
                return response;
            });

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
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
        Uri url = new("https://example.com/file.zip");
        ByteRange range = new(0, 99);
        var options = CreateDefaultOptions();
        var testData = new byte[100];
        for (var i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
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
                HttpResponseMessage response = new(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(testData)
                };
                return response;
            });

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        using MemoryStream destination = new();
        long reportedProgress = 0;
        Progress<long> progress = new(p => reportedProgress = p);

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
        Uri url = new("https://example.com/file.zip");
        ByteRange range = new(100, 199); // Non-zero start
        var options = CreateDefaultOptions();

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                // Server returns 200 OK instead of 206 Partial Content
                HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[100]) };
                return response;
            });

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient("KurioDownloader"))
            .Returns(httpClient);

        using MemoryStream destination = new();

        // Act & Assert
        var act = async () => await _handler.DownloadRangeAsync(url, range, destination, options);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*did not honor range request*");
    }

    [Fact]
    public async Task GetMetadataAsync_ReturnsCompleteMetadata()
    {
        // Arrange
        Uri url = new("https://example.com/file.zip");
        var options = CreateDefaultOptions();
        var expectedETag = "\"abc123\"";
        var expectedLastModified = DateTimeOffset.UtcNow;
        const long expectedSize = 2048;

        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
                response.Content.Headers.ContentLength = expectedSize;
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                response.Content.Headers.LastModified = expectedLastModified;
                response.Headers.ETag = new EntityTagHeaderValue(expectedETag);
                response.Headers.Add("Accept-Ranges", "bytes");
                response.Content.Headers.ContentDisposition =
                    new ContentDispositionHeaderValue("attachment") { FileName = "test-file.zip" };
                return response;
            });

        HttpClient httpClient = new(httpMessageHandlerMock.Object);
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
        Func<Task<bool>> act = async () => await _handler.SupportsRangeRequestsAsync(null!, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetFileSizeAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act & Assert
        Func<Task<long>> act = async () => await _handler.GetFileSizeAsync(null!, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadRangeAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        ByteRange range = new(0, 99);
        var options = CreateDefaultOptions();
        using MemoryStream destination = new();

        // Act & Assert
        var act = async () => await _handler.DownloadRangeAsync(null!, range, destination, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadRangeAsync_WithNonWritableStream_ThrowsArgumentException()
    {
        // Arrange
        Uri url = new("https://example.com/file.zip");
        ByteRange range = new(0, 99);
        var options = CreateDefaultOptions();
        using MemoryStream destination = new(new byte[100], false);

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
        Func<Task<ResourceMetadata>> act = async () => await _handler.GetMetadataAsync(null!, options);
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
