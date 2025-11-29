using FluentValidation.TestHelper;
using Kurio.Core.Configuration;
using Kurio.Core.Configuration.Validators;

namespace Kurio.Core.Tests.Configuration;

public sealed class RetryPolicySettingsValidatorTests
{
    private readonly RetryPolicySettingsValidator _validator = new();

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void MaxRetries_WhenOutOfRange_ShouldHaveError(int value)
    {
        var settings = new RetryPolicySettings { MaxRetries = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MaxRetries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void MaxRetries_WhenInRange_ShouldNotHaveError(int value)
    {
        var settings = new RetryPolicySettings { MaxRetries = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxRetries);
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(61.0)]
    public void InitialDelaySeconds_WhenOutOfRange_ShouldHaveError(double value)
    {
        var settings = new RetryPolicySettings { InitialDelaySeconds = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.InitialDelaySeconds);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(30.0)]
    [InlineData(60.0)]
    public void InitialDelaySeconds_WhenInRange_ShouldNotHaveError(double value)
    {
        var settings = new RetryPolicySettings { InitialDelaySeconds = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.InitialDelaySeconds);
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(301.0)]
    public void MaxDelaySeconds_WhenOutOfRange_ShouldHaveError(double value)
    {
        var settings = new RetryPolicySettings { MaxDelaySeconds = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MaxDelaySeconds);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(60.0)]
    [InlineData(300.0)]
    public void MaxDelaySeconds_WhenInRange_ShouldNotHaveError(double value)
    {
        var settings = new RetryPolicySettings { MaxDelaySeconds = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxDelaySeconds);
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(5.1)]
    public void BackoffMultiplier_WhenOutOfRange_ShouldHaveError(double value)
    {
        var settings = new RetryPolicySettings { BackoffMultiplier = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.BackoffMultiplier);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(5.0)]
    public void BackoffMultiplier_WhenInRange_ShouldNotHaveError(double value)
    {
        var settings = new RetryPolicySettings { BackoffMultiplier = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.BackoffMultiplier);
    }
}
