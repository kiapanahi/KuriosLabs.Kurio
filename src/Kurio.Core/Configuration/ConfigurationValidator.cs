using FluentValidation;
using Kurio.Core.Configuration.Validators;

namespace Kurio.Core.Configuration;

/// <summary>
///     Validates configuration settings using FluentValidation
/// </summary>
public sealed class ConfigurationValidator
{
    private readonly IValidator<KurioConfiguration> _validator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationValidator"/> class
    /// </summary>
    public ConfigurationValidator()
    {
        _validator = new KurioConfigurationValidator();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationValidator"/> class with a custom validator
    /// </summary>
    /// <param name="validator">Custom validator instance</param>
    public ConfigurationValidator(IValidator<KurioConfiguration> validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    ///     Validates the entire configuration object
    /// </summary>
    public ConfigurationValidationResult Validate(KurioConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var result = _validator.Validate(config);

        if (result.IsValid)
        {
            return ConfigurationValidationResult.Success();
        }

        var errors = result.Errors.Select(error => new ConfigurationError
        {
            PropertyPath = error.PropertyName,
            Message = error.ErrorMessage,
            CurrentValue = error.AttemptedValue,
            ExpectedConstraint = error.ErrorMessage
        }).ToList();

        return new ConfigurationValidationResult { Errors = errors };
    }
}
