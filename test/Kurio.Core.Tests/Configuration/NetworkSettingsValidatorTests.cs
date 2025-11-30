using FluentValidation.TestHelper;

using Kurio.Core.Configuration;

using KuriousLabs.Kurio.Core.Configuration.Validators;

namespace KuriousLabs.Kurio.Configuration;

public sealed class NetworkSettingsValidatorTests
{
    private readonly NetworkSettingsValidator _validator = new();

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(301)]
    public void TimeoutSeconds_WhenOutOfRange_ShouldHaveError(int value)
    {
        var settings = new NetworkSettings { TimeoutSeconds = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.TimeoutSeconds);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(300)]
    public void TimeoutSeconds_WhenInRange_ShouldNotHaveError(int value)
    {
        var settings = new NetworkSettings { TimeoutSeconds = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.TimeoutSeconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void MaxRedirects_WhenOutOfRange_ShouldHaveError(int value)
    {
        var settings = new NetworkSettings { MaxRedirects = value };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MaxRedirects);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void MaxRedirects_WhenInRange_ShouldNotHaveError(int value)
    {
        var settings = new NetworkSettings { MaxRedirects = value };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxRedirects);
    }

    [Fact]
    public void RetryPolicy_WhenInvalid_ShouldHaveError()
    {
        var settings = new NetworkSettings
        {
            RetryPolicy = new RetryPolicySettings { MaxRetries = 100 } // Invalid
        };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor("RetryPolicy.MaxRetries");
    }

    [Fact]
    public void RetryPolicy_WhenValid_ShouldNotHaveError()
    {
        var settings = new NetworkSettings
        {
            RetryPolicy = new RetryPolicySettings { MaxRetries = 3 }
        };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.RetryPolicy);
    }
}
