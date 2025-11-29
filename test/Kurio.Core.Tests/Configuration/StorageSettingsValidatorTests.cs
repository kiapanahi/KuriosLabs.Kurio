using FluentValidation.TestHelper;
using Kurio.Core.Configuration;
using Kurio.Core.Configuration.Validators;

namespace Kurio.Core.Tests.Configuration;

public sealed class StorageSettingsValidatorTests
{
    private readonly StorageSettingsValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(10 * 1024 * 1024 - 1)] // Just below 10 MB
    public void MinimumFreeSpace_WhenBelowMinimum_ShouldHaveError(long value)
    {
        var settings = new StorageSettings { MinimumFreeSpace = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MinimumFreeSpace);
    }

    [Theory]
    [InlineData(10 * 1024 * 1024)] // Exactly 10 MB
    [InlineData(100 * 1024 * 1024)] // 100 MB
    public void MinimumFreeSpace_WhenAtOrAboveMinimum_ShouldNotHaveError(long value)
    {
        var settings = new StorageSettings { MinimumFreeSpace = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MinimumFreeSpace);
    }
}
