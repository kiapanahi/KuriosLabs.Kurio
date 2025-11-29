using FluentValidation;

namespace Kurio.Core.Configuration.Validators;

/// <summary>
///     Validator for RetryPolicySettings configuration
/// </summary>
public sealed class RetryPolicySettingsValidator : AbstractValidator<RetryPolicySettings>
{
    public RetryPolicySettingsValidator()
    {
        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(0, 10)
            .WithMessage("Must be between 0 and 10");

        RuleFor(x => x.InitialDelaySeconds)
            .InclusiveBetween(0.5, 60.0)
            .WithMessage("Must be between 0.5 and 60");

        RuleFor(x => x.MaxDelaySeconds)
            .InclusiveBetween(1.0, 300.0)
            .WithMessage("Must be between 1 and 300");

        RuleFor(x => x.BackoffMultiplier)
            .InclusiveBetween(1.0, 5.0)
            .WithMessage("Must be between 1.0 and 5.0");
    }
}
