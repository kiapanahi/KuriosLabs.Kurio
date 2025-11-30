using FluentValidation;

namespace KuriousLabs.Kurio.Core.Configuration.Validators;

/// <summary>
///     Validator for NetworkSettings configuration
/// </summary>
public sealed class NetworkSettingsValidator : AbstractValidator<NetworkSettings>
{
    public NetworkSettingsValidator()
    {
        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(5, 300)
            .WithMessage("Must be between 5 and 300");

        RuleFor(x => x.MaxRedirects)
            .InclusiveBetween(0, 10)
            .WithMessage("Must be between 0 and 10");

        RuleFor(x => x.RetryPolicy)
            .SetValidator(new RetryPolicySettingsValidator());
    }
}
