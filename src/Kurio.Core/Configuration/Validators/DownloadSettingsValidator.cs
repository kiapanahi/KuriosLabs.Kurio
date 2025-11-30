using FluentValidation;

namespace Kurio.Core.Configuration.Validators;

/// <summary>
///     Validator for DownloadSettings configuration
/// </summary>
public sealed class DownloadSettingsValidator : AbstractValidator<DownloadSettings>
{
    private static readonly string[] ValidFileNamingPoliciesCaps = [.. DownloadSettings.ValidFileNamingPolicies.Select(p => p.ToUpperInvariant())];
    public DownloadSettingsValidator()
    {
        RuleFor(x => x.MaxConcurrentDownloads)
            .InclusiveBetween(1, 20)
            .WithMessage("Must be between 1 and 20");

        RuleFor(x => x.MaxConnectionsPerDownload)
            .InclusiveBetween(1, 32)
            .WithMessage("Must be between 1 and 32");

        RuleFor(x => x.MinSegmentSize)
            .InclusiveBetween(512 * 1024L, 100 * 1024 * 1024L)
            .WithMessage("Must be between 512 KB and 100 MB");

        RuleFor(x => x.SegmentBufferSize)
            .InclusiveBetween(4 * 1024, 1024 * 1024)
            .WithMessage("Must be between 4 KB and 1 MB");

        RuleFor(x => x.DefaultDirectory)
            .NotEmpty()
            .WithMessage("Cannot be empty");

        RuleFor(x => x.TempDirectory)
            .NotEmpty()
            .WithMessage("Cannot be empty");

        RuleFor(x => x.FileNamingPolicy)
            .Must(BeValidFileNamingPolicy)
            .WithMessage("Must be one of: overwrite, appendNumber, appendTimestamp, failIfExists, skipIfExists");
    }

    private static bool BeValidFileNamingPolicy(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
        {
            return false;
        }

        return ValidFileNamingPoliciesCaps.Contains(policy.ToUpperInvariant());
    }
}
