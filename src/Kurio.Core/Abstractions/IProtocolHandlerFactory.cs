namespace KuriousLabs.Kurio.Core.Abstractions;

/// <summary>
///     Factory for creating protocol handlers based on URI schemes.
/// </summary>
public interface IProtocolHandlerFactory
{
    /// <summary>
    ///     Gets a protocol handler that supports the specified URL.
    /// </summary>
    /// <param name="url">The URL to get a handler for.</param>
    /// <returns>A protocol handler that supports the URL's scheme.</returns>
    /// <exception cref="NotSupportedException">If no handler supports the URL's scheme.</exception>
    IProtocolHandler GetHandler(Uri url);

    /// <summary>
    ///     Determines whether a protocol handler exists for the specified URL.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>True if a handler exists for the URL's scheme; otherwise, false.</returns>
    bool IsSupported(Uri url);

    /// <summary>
    ///     Gets all registered protocol handlers.
    /// </summary>
    IReadOnlyCollection<IProtocolHandler> GetAllHandlers();
}
