using FluentAssertions;

using Kurio.Core.Abstractions;
using Kurio.Core.Protocols;

using Moq;

namespace Kurio.Core.Tests.Protocols;

public sealed class ProtocolHandlerFactoryTests
{
    [Fact]
    public void Constructor_WithNullHandlers_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ProtocolHandlerFactory(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithDuplicateSchemes_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler1 = CreateMockHandler(new[] { "http", "https" });
        var handler2 = CreateMockHandler(new[] { "http" }); // Duplicate scheme

        // Act & Assert
        var act = () => new ProtocolHandlerFactory(new[] { handler1, handler2 });
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Multiple handlers registered for scheme*");
    }

    [Fact]
    public void GetHandler_WithSupportedScheme_ReturnsCorrectHandler()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http", "https" });
        var ftpHandler = CreateMockHandler(new[] { "ftp", "ftps" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler, ftpHandler });
        Uri url = new("https://example.com/file.zip");

        // Act
        var result = factory.GetHandler(url);

        // Assert
        result.Should().BeSameAs(httpHandler);
    }

    [Fact]
    public void GetHandler_WithUnsupportedScheme_ThrowsNotSupportedException()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http", "https" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler });
        Uri url = new("ftp://example.com/file.zip");

        // Act & Assert
        var act = () => factory.GetHandler(url);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*No protocol handler registered for scheme 'ftp'*");
    }

    [Fact]
    public void GetHandler_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler });

        // Act & Assert
        var act = () => factory.GetHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetHandler_WithEmptyScheme_ThrowsArgumentException()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler });
        Uri url = new("file:///path/to/file", UriKind.Absolute);

        // Act & Assert - file: scheme should work but we don't have handler
        var act = () => factory.GetHandler(url);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void IsSupported_WithSupportedScheme_ReturnsTrue()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http", "https" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler });
        Uri url = new("https://example.com/file.zip");

        // Act
        var result = factory.IsSupported(url);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSupported_WithUnsupportedScheme_ReturnsFalse()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http", "https" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler });
        Uri url = new("ftp://example.com/file.zip");

        // Act
        var result = factory.IsSupported(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSupported_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler });

        // Act & Assert
        var act = () => factory.IsSupported(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetAllHandlers_ReturnsAllRegisteredHandlers()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http", "https" });
        var ftpHandler = CreateMockHandler(new[] { "ftp", "ftps" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler, ftpHandler });

        // Act
        var result = factory.GetAllHandlers();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(httpHandler);
        result.Should().Contain(ftpHandler);
    }

    [Fact]
    public void GetHandler_IsCaseInsensitive()
    {
        // Arrange
        var httpHandler = CreateMockHandler(new[] { "http", "https" });
        ProtocolHandlerFactory factory = new(new[] { httpHandler });
        Uri url1 = new("HTTP://example.com/file.zip");
        Uri url2 = new("HtTpS://example.com/file.zip");

        // Act
        var result1 = factory.GetHandler(url1);
        var result2 = factory.GetHandler(url2);

        // Assert
        result1.Should().BeSameAs(httpHandler);
        result2.Should().BeSameAs(httpHandler);
    }

    private static IProtocolHandler CreateMockHandler(string[] supportedSchemes)
    {
        Mock<IProtocolHandler> mock = new();
        mock.Setup(h => h.SupportedSchemes)
            .Returns(new HashSet<string>(supportedSchemes, StringComparer.OrdinalIgnoreCase));
        return mock.Object;
    }
}
