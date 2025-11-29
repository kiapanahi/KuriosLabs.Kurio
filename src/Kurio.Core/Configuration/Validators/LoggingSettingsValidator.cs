using FluentValidation;

namespace Kurio.Core.Configuration.Validators;

/// <summary>
///     Validator for LoggingSettings configuration
/// </summary>
public sealed class LoggingSettingsValidator : AbstractValidator<LoggingSettings>
{
    public LoggingSettingsValidator()
    {
        RuleFor(x => x.Level)
            .Must(BeValidLogLevel)
            .WithMessage("Must be one of: trace, debug, information, warning, error, critical");

        RuleFor(x => x.MaxLogFiles)
            .InclusiveBetween(1, 100)
            .WithMessage("Must be between 1 and 100");

        RuleFor(x => x.LogDirectory)
            .NotEmpty()
            .WithMessage("Cannot be empty");
    }

    private static bool BeValidLogLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return false;
        }

        var validLevels = new[] { "trace", "debug", "information", "warning", "error", "critical" };
        return validLevels.Contains(level.ToLowerInvariant());
    }
}
