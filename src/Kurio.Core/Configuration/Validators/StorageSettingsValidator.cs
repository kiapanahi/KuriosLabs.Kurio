using FluentValidation;

namespace Kurio.Core.Configuration.Validators;

/// <summary>
///     Validator for StorageSettings configuration
/// </summary>
public sealed class StorageSettingsValidator : AbstractValidator<StorageSettings>
{
    private const long MinFreeSpace = 10 * 1024 * 1024; // 10 MB

    public StorageSettingsValidator()
    {
        RuleFor(x => x.MinimumFreeSpace)
            .GreaterThanOrEqualTo(MinFreeSpace)
            .WithMessage($"Must be at least 10 MB ({MinFreeSpace} bytes)");
    }
}
