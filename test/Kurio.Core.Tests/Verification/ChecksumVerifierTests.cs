using System.Text;
using Kurio.Core.Models;
using Kurio.Core.Verification;
using Xunit;

namespace Kurio.Core.Tests.Verification;

/// <summary>
/// Unit tests for <see cref="ChecksumVerifier"/>.
/// </summary>
public sealed class ChecksumVerifierTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ChecksumVerifier _verifier;

    public ChecksumVerifierTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"kurio_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _verifier = new ChecksumVerifier();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithFile_MD5_ReturnsCorrectHash()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);
        var expectedHash = "65a8e27d8879283831b664bd8b7f0ad4"; // MD5 of "Hello, World!"

        // Act
        var actualHash = await _verifier.CalculateChecksumAsync(testFile, ChecksumAlgorithm.MD5);

        // Assert
        Assert.Equal(expectedHash, actualHash, ignoreCase: true);
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithFile_SHA1_ReturnsCorrectHash()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);
        var expectedHash = "0a0a9f2a6772942557ab5355d76af442f8f65e01"; // SHA1

        // Act
        var actualHash = await _verifier.CalculateChecksumAsync(testFile, ChecksumAlgorithm.SHA1);

        // Assert
        Assert.Equal(expectedHash, actualHash, ignoreCase: true);
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithFile_SHA256_ReturnsCorrectHash()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);
        var expectedHash = "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f"; // SHA256

        // Act
        var actualHash = await _verifier.CalculateChecksumAsync(testFile, ChecksumAlgorithm.SHA256);

        // Assert
        Assert.Equal(expectedHash, actualHash, ignoreCase: true);
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithFile_SHA512_ReturnsCorrectHash()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);
        var expectedHash = "374d794a95cdcfd8b35993185fef9ba368f160d8daf432d08ba9f1ed1e5abe6cc69291e0fa2fe0006a52570ef18c19def4e617c33ce52ef0a6e5fbe318cb0387"; // SHA512

        // Act
        var actualHash = await _verifier.CalculateChecksumAsync(testFile, ChecksumAlgorithm.SHA512);

        // Assert
        Assert.Equal(expectedHash, actualHash, ignoreCase: true);
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithStream_ReturnsCorrectHash()
    {
        // Arrange
        var content = "Hello, World!";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var expectedHash = "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f"; // SHA256

        // Act
        var actualHash = await _verifier.CalculateChecksumAsync(stream, ChecksumAlgorithm.SHA256);

        // Assert
        Assert.Equal(expectedHash, actualHash, ignoreCase: true);
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _verifier.CalculateChecksumAsync(nonExistentFile, ChecksumAlgorithm.SHA256));
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _verifier.CalculateChecksumAsync((Stream)null!, ChecksumAlgorithm.SHA256));
    }

    [Fact]
    public async Task VerifyFileAsync_WithMatchingChecksum_ReturnsValidResult()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);
        var expectedHash = "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f"; // SHA256

        // Act
        var result = await _verifier.VerifyFileAsync(testFile, expectedHash, ChecksumAlgorithm.SHA256);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ChecksumAlgorithm.SHA256, result.Algorithm);
        Assert.Equal(expectedHash, result.CalculatedChecksum, ignoreCase: true);
        Assert.Equal(expectedHash, result.ExpectedChecksum);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyFileAsync_WithMismatchedChecksum_ReturnsInvalidResult()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);
        var wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = await _verifier.VerifyFileAsync(testFile, wrongHash, ChecksumAlgorithm.SHA256);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotEqual(result.CalculatedChecksum, result.ExpectedChecksum, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyFileAsync_CaseInsensitive_ReturnsValidResult()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);
        var expectedHashUpperCase = "DFFD6021BB2BD5B0AF676290809EC3A53191DD81C7F70A4B28688A362182986F";

        // Act
        var result = await _verifier.VerifyFileAsync(testFile, expectedHashUpperCase, ChecksumAlgorithm.SHA256);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ParseChecksumFileAsync_GNU_Format_ParsesCorrectly()
    {
        // Arrange
        var checksumFile = Path.Combine(_testDirectory, "checksums.md5");
        var content = @"d41d8cd98f00b204e9800998ecf8427e *file1.txt
098f6bcd4621d373cade4e832627b4f6  file2.txt
5d41402abc4b2a76b9719d911017c592 file3.txt";
        await File.WriteAllTextAsync(checksumFile, content);

        // Act
        var checksums = await _verifier.ParseChecksumFileAsync(checksumFile);

        // Assert
        Assert.Equal(3, checksums.Count);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", checksums["file1.txt"]);
        Assert.Equal("098f6bcd4621d373cade4e832627b4f6", checksums["file2.txt"]);
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", checksums["file3.txt"]);
    }

    [Fact]
    public async Task ParseChecksumFileAsync_WithComments_SkipsComments()
    {
        // Arrange
        var checksumFile = Path.Combine(_testDirectory, "checksums.sha256");
        var content = @"# This is a comment
d41d8cd98f00b204e9800998ecf8427e file1.txt
# Another comment
098f6bcd4621d373cade4e832627b4f6 file2.txt";
        await File.WriteAllTextAsync(checksumFile, content);

        // Act
        var checksums = await _verifier.ParseChecksumFileAsync(checksumFile);

        // Assert
        Assert.Equal(2, checksums.Count);
        Assert.DoesNotContain("#", checksums.Keys.First());
    }

    [Fact]
    public async Task ParseChecksumFileAsync_WithEmptyLines_SkipsEmptyLines()
    {
        // Arrange
        var checksumFile = Path.Combine(_testDirectory, "checksums.sha256");
        var content = @"d41d8cd98f00b204e9800998ecf8427e file1.txt

098f6bcd4621d373cade4e832627b4f6 file2.txt
";
        await File.WriteAllTextAsync(checksumFile, content);

        // Act
        var checksums = await _verifier.ParseChecksumFileAsync(checksumFile);

        // Assert
        Assert.Equal(2, checksums.Count);
    }

    [Fact]
    public async Task ParseChecksumFileAsync_WithFilenamesContainingSpaces_ParsesCorrectly()
    {
        // Arrange
        var checksumFile = Path.Combine(_testDirectory, "checksums.md5");
        var content = "d41d8cd98f00b204e9800998ecf8427e my file with spaces.txt";
        await File.WriteAllTextAsync(checksumFile, content);

        // Act
        var checksums = await _verifier.ParseChecksumFileAsync(checksumFile);

        // Assert
        Assert.Single(checksums);
        Assert.True(checksums.ContainsKey("my file with spaces.txt"));
    }

    [Fact]
    public void ExtractChecksumFromHeaders_WithContentMD5_ReturnsCorrectChecksum()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Content-MD5", new[] { "d41d8cd98f00b204e9800998ecf8427e" } }
        };

        // Act
        var result = _verifier.ExtractChecksumFromHeaders(headers);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ChecksumAlgorithm.MD5, result.Value.Algorithm);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result.Value.Checksum);
    }

    [Fact]
    public void ExtractChecksumFromHeaders_WithXChecksumSHA256_ReturnsCorrectChecksum()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "X-Checksum-SHA256", new[] { "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f" } }
        };

        // Act
        var result = _verifier.ExtractChecksumFromHeaders(headers);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ChecksumAlgorithm.SHA256, result.Value.Algorithm);
    }

    [Fact]
    public void ExtractChecksumFromHeaders_WithDigestHeader_ParsesCorrectly()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Digest", new[] { "SHA-256=31qweTdqed7qeqe78qi=" } }
        };

        // Act
        var result = _verifier.ExtractChecksumFromHeaders(headers);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ChecksumAlgorithm.SHA256, result.Value.Algorithm);
    }

    [Fact]
    public void ExtractChecksumFromHeaders_WithNoChecksumHeaders_ReturnsNull()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Content-Type", new[] { "application/octet-stream" } },
            { "Content-Length", new[] { "1024" } }
        };

        // Act
        var result = _verifier.ExtractChecksumFromHeaders(headers);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CalculateChecksumAsync_WithLargeFile_CompletesSuccessfully()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "large.bin");
        var fileSize = 10 * 1024 * 1024; // 10 MB
        var buffer = new byte[fileSize];
        new Random(42).NextBytes(buffer);
        await File.WriteAllBytesAsync(testFile, buffer);

        // Act
        var hash = await _verifier.CalculateChecksumAsync(testFile, ChecksumAlgorithm.SHA256);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // SHA256 produces 64 hex characters
    }
}
