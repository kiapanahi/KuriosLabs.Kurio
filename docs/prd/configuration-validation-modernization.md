# Product Requirements Document: Configuration Validation Modernization

**Version:** 1.0  
**Date:** December 2025  
**Status:** Draft  
**Author:** Kurio Development Team

---

## Executive Summary

This document outlines the modernization of Kurio's configuration validation system from a manual, procedural approach to a declarative, maintainable solution using FluentValidation. This change will improve code quality, testability, and maintainability while providing better error messages and validation flexibility.

---

## 1. Problem Statement

### Current Issues

The existing `ConfigurationValidator` class has several drawbacks:

1. **Poor Maintainability**: 200+ lines of repetitive if-else validation logic
2. **Limited Testability**: Hard to test individual validation rules in isolation
3. **Inflexible**: Adding new rules requires modifying the validator class
4. **Verbose Error Messages**: Manual error message construction is repetitive
5. **Not Reusable**: Validation logic is tightly coupled to the validator class
6. **No Composition**: Cannot easily compose or share validation rules

### Example of Current Approach

```csharp
if (settings.MaxConcurrentDownloads < 1 || settings.MaxConcurrentDownloads > 20)
{
    errors.Add(new ConfigurationError
    {
        PropertyPath = "Downloads.MaxConcurrentDownloads",
        Message = "Must be between 1 and 20",
        CurrentValue = settings.MaxConcurrentDownloads,
        ExpectedConstraint = "1 ? x ? 20"
    });
}
```

---

## 2. Proposed Solution

### Use FluentValidation

FluentValidation is a popular .NET validation library that provides:

- **Declarative Syntax**: Define rules in a fluent, readable manner
- **Separation of Concerns**: Each validator is a separate class
- **Composability**: Easily reuse and combine validators
- **Rich Rule Set**: Built-in rules for common scenarios (range, not empty, regex, etc.)
- **Custom Rules**: Easy to create custom validation logic
- **Better Testing**: Test individual rules in isolation
- **Dependency Injection**: First-class DI support

### Benefits

1. **Readability**: Rules are self-documenting
2. **Maintainability**: Each validator is a focused class
3. **Testability**: Easy to write unit tests for specific rules
4. **Extensibility**: Simple to add new rules without modifying existing code
5. **Consistency**: Standardized approach across the codebase
6. **Better Error Messages**: Automatic property path resolution and formatted messages

---

## 3. Implementation Design

### 3.1 Dependencies

Add FluentValidation to `Directory.Packages.props`:

```xml
<PackageVersion Include="FluentValidation" Version="11.13.0" />
<PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.13.0" />
```

### 3.2 Validator Structure

Create separate validator classes for each configuration section:

```
src/Kurio.Core/Configuration/Validators/
??? KurioConfigurationValidator.cs
??? DownloadSettingsValidator.cs
??? NetworkSettingsValidator.cs
??? RetryPolicySettingsValidator.cs
??? StorageSettingsValidator.cs
??? LoggingSettingsValidator.cs
??? CategoryRuleValidator.cs
```

### 3.3 Example Implementation

**DownloadSettingsValidator.cs**

```csharp
using FluentValidation;

namespace Kurio.Core.Configuration.Validators;

public sealed class DownloadSettingsValidator : AbstractValidator<DownloadSettings>
{
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
        var validPolicies = new[] { "overwrite", "appendNumber", "appendTimestamp", "failIfExists", "skipIfExists" };
        return validPolicies.Contains(policy?.ToLowerInvariant());
    }
}
```

**KurioConfigurationValidator.cs**

```csharp
using FluentValidation;

namespace Kurio.Core.Configuration.Validators;

public sealed class KurioConfigurationValidator : AbstractValidator<KurioConfiguration>
{
    public KurioConfigurationValidator()
    {
        RuleFor(x => x.Downloads)
            .SetValidator(new DownloadSettingsValidator());

        RuleFor(x => x.Network)
            .SetValidator(new NetworkSettingsValidator());

        RuleFor(x => x.Storage)
            .SetValidator(new StorageSettingsValidator());

        RuleFor(x => x.Logging)
            .SetValidator(new LoggingSettingsValidator());
    }
}
```

### 3.4 Updated ConfigurationValidator

Replace the current implementation with a FluentValidation wrapper:

```csharp
public sealed class ConfigurationValidator
{
    private readonly IValidator<KurioConfiguration> _validator;

    public ConfigurationValidator()
    {
        _validator = new KurioConfigurationValidator();
    }

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
```

### 3.5 Dependency Injection Setup

For services that use DI, register validators:

```csharp
services.AddValidatorsFromAssemblyContaining<KurioConfigurationValidator>();
```

---

## 4. Migration Strategy

### Phase 1: Add Dependencies
- Add FluentValidation NuGet packages
- Update `Directory.Packages.props`

### Phase 2: Create Validators
- Create validator classes for each configuration section
- Implement validation rules using FluentValidation

### Phase 3: Update ConfigurationValidator
- Replace manual validation logic with FluentValidation wrapper
- Maintain existing `ConfigurationValidationResult` interface for backward compatibility

### Phase 4: Testing
- Write unit tests for each validator
- Ensure all existing tests pass
- Add additional test cases for edge scenarios

### Phase 5: Cleanup
- Remove old validation code
- Update documentation

---

## 5. Testing Strategy

### Unit Tests

```csharp
public class DownloadSettingsValidatorTests
{
    private readonly DownloadSettingsValidator _validator = new();

    [Fact]
    public void MaxConcurrentDownloads_WhenLessThan1_ShouldHaveError()
    {
        var settings = new DownloadSettings { MaxConcurrentDownloads = 0 };
        var result = _validator.TestValidate(settings);
        result.ShouldHaveValidationErrorFor(x => x.MaxConcurrentDownloads);
    }

    [Fact]
    public void MaxConcurrentDownloads_WhenValid_ShouldNotHaveError()
    {
        var settings = new DownloadSettings { MaxConcurrentDownloads = 5 };
        var result = _validator.TestValidate(settings);
        result.ShouldNotHaveValidationErrorFor(x => x.MaxConcurrentDownloads);
    }
}
```

---

## 6. Success Criteria

- All existing configuration validation tests pass
- Code is more readable and maintainable
- Validation logic is easily testable
- No regression in validation behavior
- Test coverage remains >85%

---

## 7. Future Enhancements

- **Conditional Validation**: Rules that depend on other property values
- **Async Validation**: For validators that need to check external resources
- **Custom Validators**: Domain-specific validation logic
- **Localization**: Multi-language error messages

---

## 8. References

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [.NET Validation Patterns](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation)

---

**Document Status**: ? Ready for Implementation  
**Next Steps**: Create branch ? Implement ? Test ? PR
