using System.Runtime.InteropServices;

namespace Kurio.Core.Storage;

/// <summary>
/// Provides platform-specific path information
/// </summary>
public interface IPlatformPathProvider
{
    /// <summary>
    /// Gets the default downloads directory for the current platform
    /// </summary>
    string GetDefaultDownloadsDirectory();

    /// <summary>
    /// Gets the application data directory for storing config and state
    /// </summary>
    string GetAppDataDirectory();

    /// <summary>
    /// Gets the temporary directory for downloads
    /// </summary>
    string GetTempDirectory();

    /// <summary>
    /// Expands a path with environment variables and user home directory
    /// </summary>
    /// <param name="path">Path to expand (supports ~ for home directory)</param>
    string ExpandPath(string path);

    /// <summary>
    /// Gets invalid filename characters for the current platform
    /// </summary>
    char[] GetInvalidFileNameChars();

    /// <summary>
    /// Checks if a path is valid for the current platform
    /// </summary>
    bool IsValidPath(string path);
}

/// <summary>
/// Default implementation of IPlatformPathProvider
/// </summary>
public sealed class PlatformPathProvider : IPlatformPathProvider
{
    private readonly string _homeDirectory;
    private readonly string _downloadsDirectory;
    private readonly string _appDataDirectory;
    private readonly string _tempDirectory;

    public PlatformPathProvider()
    {
        _homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _downloadsDirectory = Path.Combine(_homeDirectory, "Downloads");
            _appDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Kurio");
            _tempDirectory = Path.Combine(Path.GetTempPath(), "Kurio");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _downloadsDirectory = Path.Combine(_homeDirectory, "Downloads");
            _appDataDirectory = Path.Combine(_homeDirectory, "Library", "Application Support", "Kurio");
            _tempDirectory = Path.Combine(_homeDirectory, "Library", "Caches", "Kurio");
        }
        else // Linux and other Unix-like systems
        {
            _downloadsDirectory = Path.Combine(_homeDirectory, "Downloads");
            _appDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) is { Length: > 0 } configHome
                    ? configHome
                    : Path.Combine(_homeDirectory, ".config"),
                "kurio");
            _tempDirectory = Path.Combine("/tmp", "kurio");
        }
    }

    public string GetDefaultDownloadsDirectory() => _downloadsDirectory;

    public string GetAppDataDirectory() => _appDataDirectory;

    public string GetTempDirectory() => _tempDirectory;

    public string ExpandPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Expand ~ to home directory
        if (path.StartsWith("~/") || path == "~")
        {
            path = path == "~"
                ? _homeDirectory
                : Path.Combine(_homeDirectory, path[2..]);
        }

        // Expand environment variables
        path = Environment.ExpandEnvironmentVariables(path);

        return Path.GetFullPath(path);
    }

    public char[] GetInvalidFileNameChars()
    {
        return Path.GetInvalidFileNameChars();
    }

    public bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var expanded = ExpandPath(path);
            _ = Path.GetFullPath(expanded);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
