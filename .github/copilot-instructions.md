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
- Support for downloading streaming media
- Ability to import and export download lists
- Automatic updates to ensure the latest features and security patches
- Support for proxy servers
- Extensive documentation and tutorials for users
- Open-source with contributions from the community

## Prompts and instructions

- For any new feature or bug fix, create a new prd file in the `docs/prd/` directory outlining the requirements and specifications.
- From the PRD file generate user stories and tasks in Github Issues.
- When implementing features, ensure to write unit tests and integration tests as needed.

## Github repository

The GitHub repository for Kurio can be found at:

- HTTPS format: https://github.com/kiapanahi/KuriosLabs.Kurio.git
- SSH format: git@github.com:kiapanahi/KuriosLabs.Kurio.git

## Language and platform

The project is primarily written in C# and .NET, making it cross-platform and compatible with Windows, macOS, and Linux operating systems.

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
- Ensure cross-platform compatibility across Windows, macOS, and Linux.
- Use .NET 10.0 or later for all new code.

### Git

- Always adhere to the 50/72 rule for commit messages.
- Follow the Gitflow workflow for branching and merging.
- Write clear and concise commit messages following the Conventional Commits specification.
- Create pull requests for all changes, with appropriate descriptions and linked issues.
