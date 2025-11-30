using FluentValidation;

using Kurio.Core.Configuration.Validators;

namespace KuriousLabs.Kurio.Core.Configuration.Validators;

/// <summary>
///     Root validator for KurioConfiguration
/// </summary>
public sealed class KurioConfigurationValidator : AbstractValidator<KurioConfiguration>
{
    public KurioConfigurationValidator()
    {
        RuleFor(x => x.Downloads)
            .NotNull()
            .SetValidator(new DownloadSettingsValidator());

        RuleFor(x => x.Network)
            .NotNull()
            .SetValidator(new NetworkSettingsValidator());

        RuleFor(x => x.Storage)
            .NotNull()
            .SetValidator(new StorageSettingsValidator());

        RuleFor(x => x.Logging)
            .NotNull()
            .SetValidator(new LoggingSettingsValidator());
    }
}
