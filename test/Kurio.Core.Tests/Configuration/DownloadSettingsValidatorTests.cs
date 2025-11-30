using FluentValidation.TestHelper;

using Kurio.Core.Configuration;
using Kurio.Core.Configuration.Validators;

namespace Kurio.Core.Tests.Configuration;

public sealed class DownloadSettingsValidatorTests
{
    private readonly DownloadSettingsValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void MaxConcurrentDownloads_WhenOutOfRange_ShouldHaveError(int value)
    {
        var settings = new DownloadSettings { MaxConcurrentDownloads = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MaxConcurrentDownloads);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    public void MaxConcurrentDownloads_WhenInRange_ShouldNotHaveError(int value)
    {
        var settings = new DownloadSettings { MaxConcurrentDownloads = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxConcurrentDownloads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(33)]
    public void MaxConnectionsPerDownload_WhenOutOfRange_ShouldHaveError(int value)
    {
        var settings = new DownloadSettings { MaxConnectionsPerDownload = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MaxConnectionsPerDownload);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(32)]
    public void MaxConnectionsPerDownload_WhenInRange_ShouldNotHaveError(int value)
    {
        var settings = new DownloadSettings { MaxConnectionsPerDownload = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxConnectionsPerDownload);
    }

    [Theory]
    [InlineData(512 * 1024 - 1)] // Just below minimum
    [InlineData(100 * 1024 * 1024 + 1)] // Just above maximum
    public void MinSegmentSize_WhenOutOfRange_ShouldHaveError(long value)
    {
        var settings = new DownloadSettings { MinSegmentSize = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MinSegmentSize);
    }

    [Theory]
    [InlineData(512 * 1024)] // Minimum
    [InlineData(1024 * 1024)] // 1 MB
    [InlineData(100 * 1024 * 1024)] // Maximum
    public void MinSegmentSize_WhenInRange_ShouldNotHaveError(long value)
    {
        var settings = new DownloadSettings { MinSegmentSize = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MinSegmentSize);
    }

    [Theory]
    [InlineData(4 * 1024 - 1)] // Just below minimum
    [InlineData(1024 * 1024 + 1)] // Just above maximum
    public void SegmentBufferSize_WhenOutOfRange_ShouldHaveError(int value)
    {
        var settings = new DownloadSettings { SegmentBufferSize = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.SegmentBufferSize);
    }

    [Theory]
    [InlineData(4 * 1024)] // Minimum
    [InlineData(8192)] // 8 KB
    [InlineData(1024 * 1024)] // Maximum
    public void SegmentBufferSize_WhenInRange_ShouldNotHaveError(int value)
    {
        var settings = new DownloadSettings { SegmentBufferSize = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.SegmentBufferSize);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void DefaultDirectory_WhenEmpty_ShouldHaveError(string? value)
    {
        var settings = new DownloadSettings { DefaultDirectory = value! };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.DefaultDirectory);
    }

    [Fact]
    public void DefaultDirectory_WhenValid_ShouldNotHaveError()
    {
        var settings = new DownloadSettings { DefaultDirectory = "~/Downloads" };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.DefaultDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void TempDirectory_WhenEmpty_ShouldHaveError(string? value)
    {
        var settings = new DownloadSettings { TempDirectory = value! };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.TempDirectory);
    }

    [Fact]
    public void TempDirectory_WhenValid_ShouldNotHaveError()
    {
        var settings = new DownloadSettings { TempDirectory = "~/.kurio/temp" };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.TempDirectory);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("INVALID")]
    [InlineData("")]
    [InlineData(null)]
    public void FileNamingPolicy_WhenInvalid_ShouldHaveError(string? value)
    {
        var settings = new DownloadSettings { FileNamingPolicy = value! };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.FileNamingPolicy);
    }

    [Theory]
    [InlineData("overwrite")]
    [InlineData("appendNumber")]
    [InlineData("appendTimestamp")]
    [InlineData("failIfExists")]
    [InlineData("skipIfExists")]
    [InlineData("OVERWRITE")] // Case insensitive
    [InlineData("AppendNumber")] // Case insensitive
    public void FileNamingPolicy_WhenValid_ShouldNotHaveError(string value)
    {
        var settings = new DownloadSettings { FileNamingPolicy = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.FileNamingPolicy);
    }
}
