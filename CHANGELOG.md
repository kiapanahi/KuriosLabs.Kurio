# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.12.1] - 2025-01-XX

### Changed
- **PERFORMANCE**: Migrated entire codebase to use `LoggerMessageAttribute` for high-performance logging
  - Refactored 10 files with 110+ log methods
  - Replaced all `Console.WriteLine` with structured logging in DownloadEngine
  - Added `.ConfigureAwait(false)` to all async calls in non-UI code (Core, Server, Cli)
  - Expected 10-30% reduction in logging overhead
  - Eliminates boxing/unboxing of value types in log calls
  - Pre-parsed message templates for faster formatting

### Added
- 10 new `*LogMessages.cs` files using source-generated logging
- Comprehensive EventId allocation (1000-9999) organized by module:
  - ErrorHandling: 1000-2999 (ErrorClassifier, CircuitBreaker, RetryHandler)
  - Persistence: 3000-3999 (JsonStatePersistence)
  - Engine: 4000-4999 (DownloadEngine, SegmentManager)
  - Protocols: 5000-5999 (HttpProtocolHandler)
  - Statistics: 6000-6999 (StatisticsService)
  - Configuration: 7000-7999 (ConfigurationService)
  - Server: 8000-8999 (DownloadsController)
  - Avalonia: 9000-9099 (App)

### Improved
- Type-safe logging parameters with compile-time validation
- Centralized log message definitions for better maintainability
- Better structured logging for download lifecycle, segment operations, and state persistence
- Resolved method ambiguity between CircuitBreaker and RetryHandler log messages

### Documentation
- Added `docs/REFACTORING_GUIDE.md` - Comprehensive logging migration guide
- Added `docs/implementation/logging-refactoring-summary.md` - Complete refactoring summary

### Technical Details
- Uses source generators for zero-overhead logging
- No breaking changes - internal implementation only
- Fully backward compatible with existing log consumers
- Cross-platform compatible (.NET 10, Windows/macOS/Linux)

## [1.12.0] - 2025-12-XX

### Added
- FluentValidation-based configuration validation system
- 6 specialized validator classes for different configuration sections
- Comprehensive test suite with 60+ test methods for validation
- PRD and implementation documentation for validation modernization

### Changed
- **BREAKING IMPROVEMENT**: Replaced manual configuration validation with FluentValidation
  - Reduced ConfigurationValidator from 200+ lines to ~50 lines
  - Improved maintainability and testability
  - Backward compatible - existing API preserved
- Updated validation error messages for better clarity

### Dependencies
- Added FluentValidation 11.13.0
- Added FluentValidation.DependencyInjectionExtensions 11.13.0

### Documentation
- Added `docs/prd/configuration-validation-modernization.md`
- Added `docs/implementation/configuration-validation-modernization-summary.md`

## [1.11.2] - Previous Release

### Previous changes
(To be documented)

---

## Version History Format

### Types of Changes
- **Added** for new features
- **Changed** for changes in existing functionality
- **Deprecated** for soon-to-be removed features
- **Removed** for now removed features
- **Fixed** for any bug fixes
- **Security** in case of vulnerabilities

### Versioning Rules
- **MAJOR** version (X.0.0): Breaking changes, incompatible API changes
- **MINOR** version (x.Y.0): New features, backward compatible additions
- **PATCH** version (x.y.Z): Bug fixes, backward compatible fixes
