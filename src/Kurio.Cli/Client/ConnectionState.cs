namespace KuriousLabs.Kurio.Cli.Client;

/// <summary>
/// Connection state of the API client.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Not connected to the server.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Attempting to connect to the server.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connected to the server.
    /// </summary>
    Connected,

    /// <summary>
    /// Lost connection, attempting to reconnect.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Connection error occurred.
    /// </summary>
    Error
}
