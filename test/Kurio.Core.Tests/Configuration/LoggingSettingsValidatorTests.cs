using FluentValidation.TestHelper;

using Kurio.Core.Configuration;
using Kurio.Core.Configuration.Validators;

namespace Kurio.Core.Tests.Configuration;

public sealed class LoggingSettingsValidatorTests
{
    private readonly LoggingSettingsValidator _validator = new();

    [Theory]
    [InlineData("invalid")]
    [InlineData("INVALID")]
    [InlineData("")]
    [InlineData(null)]
    public void Level_WhenInvalid_ShouldHaveError(string? value)
    {
        var settings = new LoggingSettings { Level = value! };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.Level);
    }

    [Theory]
    [InlineData("trace")]
    [InlineData("debug")]
    [InlineData("information")]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("critical")]
    [InlineData("TRACE")] // Case insensitive
    [InlineData("Information")] // Case insensitive
    public void Level_WhenValid_ShouldNotHaveError(string value)
    {
        var settings = new LoggingSettings { Level = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.Level);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void MaxLogFiles_WhenOutOfRange_ShouldHaveError(int value)
    {
        var settings = new LoggingSettings { MaxLogFiles = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MaxLogFiles);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void MaxLogFiles_WhenInRange_ShouldNotHaveError(int value)
    {
        var settings = new LoggingSettings { MaxLogFiles = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxLogFiles);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void LogDirectory_WhenEmpty_ShouldHaveError(string? value)
    {
        var settings = new LoggingSettings { LogDirectory = value! };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.LogDirectory);
    }

    [Fact]
    public void LogDirectory_WhenValid_ShouldNotHaveError()
    {
        var settings = new LoggingSettings { LogDirectory = "~/.kurio/logs" };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.LogDirectory);
    }
}
