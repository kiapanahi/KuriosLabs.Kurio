# Kurio

Kurio is a download manager with the following features:

- Pause and resume downloads
- Support for multiple protocols (HTTP, HTTPS, FTP)
- Download scheduling and queue management
- Support for downloading large files
- Multiple simultaneous downloads
- Automatic file segmentation
- Multi-threaded downloading for faster speeds
- Support for downloading from file hosting services
- Cross-platform compatibility (Windows, macOS, Linux)
- Command-line interface for advanced users (TUI)
- Browser integration for popular browsers (Chrome, Firefox, Edge)
- Support for plugins and extensions to enhance functionality
- Download artifacts verification (checksums)
- Download acceleration using multiple connections
- Download history and statistics
- link capturing from clipboard
- Customizable download categories and organization
- Support for downloading streaming media (yt-dlp, ffmpeg, m3u8, etc.)
- Ability to import and export download lists
- Automatic updates to ensure the latest features and security patches
- Support for proxy servers
- Extensive documentation and tutorials for users
- Open-source with contributions from the community

## Prompts and instructions

- For any new feature or bug fix, create a new prd file in the `docs/prd/` directory outlining the requirements and
  specifications.
- From the PRD file generate user stories and tasks in Github Issues.
- When implementing features, ensure to write unit tests and integration tests as needed.
- **CRITICAL**: Always update the version in `Directory.Build.props` BEFORE creating a pull request:
    - **MINOR version** (x.Y.0) for new features that are backward compatible
    - **PATCH version** (x.y.Z) for bug fixes and minor improvements
    - **MAJOR version** (X.0.0) for breaking changes
    - This is mandatory and must not be forgotten

## Github repository

The GitHub repository for Kurio can be found at:

- HTTPS format: https://github.com/kiapanahi/KuriosLabs.Kurio.git
- SSH format: git@github.com:kiapanahi/KuriosLabs.Kurio.git

## Language and platform

The project is primarily written in C# and .NET, making it cross-platform and compatible with Windows, macOS, and Linux
operating systems.

### Project structure

The project is organized into the following directories and files:

- `src/`: Contains the source code of the application.
- `docs/`: Contains documentation and user guides.
- `test/`: Contains unit and integration tests.
- `assets/`: Contains images, icons, and other media files used in the application.
- `config/`: Contains configuration files for different environments.
- `build/`: Contains scripts and files related to building and packaging the application.
- `README.md`: The main readme file with an overview of the project.
- `LICENSE`: The license file for the project.
- `CONTRIBUTING.md`: Guidelines for contributing to the project.
- `CHANGELOG.md`: A log of changes made in each version of the application.
- `.github/`: Contains GitHub-specific files such as issue templates and workflows.

### Constraints

- Always use the latest C# language features and best practices.
- For web service components, use ASP.NET Core.
- ALWAYS use minimal APIs when using ASP.NET Core.
- Ensure cross-platform compatibility across Windows, macOS, and Linux.
- Use .NET 10.0 or later for all new code.
- Always use centralized package management via NuGet.
- Add all the new dependencies to the `Directory.Packages.props` file.
- Add all the common project properties to the `Directory.Build.props` file.
- Use semver.org for versioning.
- Add version to all project files using the `Version` property in the `Directory.Build.props` file.

### Git

- Always adhere to the 50/72 rule for commit messages.
- Follow the Gitflow workflow for branching and merging.
- Always create a new branch for new features and bug fixes.
- Write clear and concise commit messages following the Conventional Commits specification.
- Create pull requests for all changes, with appropriate descriptions and linked issues.

## Versioning

The project follows semantic versioning (semver.org) for versioning. The version number is specified in the
`Directory.Build.props` file and is applied to all project files during the build process.

**Version Update Rules (MANDATORY):**

1. Update version in `Directory.Build.props` BEFORE committing any feature or bug fix
2. Follow semantic versioning strictly:
    - **MAJOR** (X.0.0): Breaking changes, incompatible API changes
    - **MINOR** (x.Y.0): New features, backward compatible additions
    - **PATCH** (x.y.Z): Bug fixes, backward compatible fixes
3. Include version bump in a separate commit with message: `chore: bump version to X.Y.Z`
4. Never create a pull request without updating the version number first

## References

For reference implementation and inspiration, you can check out the following projects:

- [aria2](https://github.com/aria2/aria2)
- [pyload](https://github.com/pyload/pyload)
- [gopeed](https://github.com/GopeedLab/gopeed)
- [brisk](https://github.com/BrisklyDev/brisk)

Find inspiration around the download manager engine, segmentation, scheduling, and multi-protocol support.