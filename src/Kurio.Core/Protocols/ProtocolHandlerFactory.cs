using Kurio.Core.Abstractions;

namespace Kurio.Core.Protocols;

/// <summary>
///     Factory implementation for creating protocol handlers.
/// </summary>
public sealed class ProtocolHandlerFactory : IProtocolHandlerFactory
{
    private readonly Dictionary<string, IProtocolHandler> _handlerCache;
    private readonly IReadOnlyCollection<IProtocolHandler> _handlers;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProtocolHandlerFactory" /> class.
    /// </summary>
    /// <param name="handlers">Collection of all registered protocol handlers.</param>
    public ProtocolHandlerFactory(IEnumerable<IProtocolHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        _handlers = handlers.ToList().AsReadOnly();
        _handlerCache = new Dictionary<string, IProtocolHandler>(StringComparer.OrdinalIgnoreCase);

        // Build cache of scheme -> handler mappings
        foreach (var handler in _handlers)
        {
            foreach (var scheme in handler.SupportedSchemes)
            {
                if (_handlerCache.ContainsKey(scheme))
                {
                    throw new InvalidOperationException(
                        $"Multiple handlers registered for scheme '{scheme}'. " +
                        $"Each scheme can only be handled by one protocol handler.");
                }

                _handlerCache[scheme] = handler;
            }
        }
    }

    /// <inheritdoc />
    public IProtocolHandler GetHandler(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (string.IsNullOrEmpty(url.Scheme))
        {
            throw new ArgumentException("URL must have a valid scheme.", nameof(url));
        }

        if (_handlerCache.TryGetValue(url.Scheme, out var handler))
        {
            return handler;
        }

        throw new NotSupportedException(
            $"No protocol handler registered for scheme '{url.Scheme}'. " +
            $"Supported schemes: {string.Join(", ", _handlerCache.Keys)}");
    }

    /// <inheritdoc />
    public bool IsSupported(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        return !string.IsNullOrEmpty(url.Scheme) &&
               _handlerCache.ContainsKey(url.Scheme);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IProtocolHandler> GetAllHandlers()
    {
        return _handlers;
    }
}
