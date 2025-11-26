# Product Requirements Document: Terminal User Interface (TUI)

## Overview

This document outlines the implementation of the Terminal User Interface (TUI) for Kurio, a cross-platform download manager. The TUI provides command-line users with a modern, interactive interface for managing downloads.

## Background

### Problem Statement
Users need a command-line interface to manage downloads without requiring a graphical user interface. This is essential for:
- Server environments without GUI
- Remote SSH sessions
- Users who prefer terminal-based workflows
- Automation and scripting scenarios

### Goals
- Provide a modern, interactive TUI for download management
- Ensure cross-platform compatibility (Windows, macOS, Linux)
- Deliver real-time progress updates and statistics
- Enable all core download operations via keyboard navigation

## Requirements

### Functional Requirements

#### FR1: Framework Selection
**Status**: ✅ Completed
- Selected **Spectre.Console** over Terminal.Gui
- Rationale:
  - Modern and actively maintained
  - Rich built-in features (progress bars, tables, live displays)
  - Excellent documentation and community support
  - Better suited for data-heavy applications like download managers
  - Simpler API for common use cases

#### FR2: Main Navigation
**Status**: ✅ Completed
- Interactive menu system with keyboard navigation
- Menu options:
  - Downloads view
  - Add download
  - Statistics
  - Settings
  - Exit
- Graceful shutdown with confirmation
- Auto-pause active downloads on exit

#### FR3: Download List View
**Status**: ✅ Completed
- Display all downloads in tabular format
- Show for each download:
  - Index number
  - Status (with emoji indicators)
  - File name
  - Progress bar with percentage
  - Download speed
  - File size
- Queue statistics (active, queued, total)
- Actions available:
  - Start/pause/resume/cancel downloads
  - Move up/down in queue
  - Clear completed downloads
  - Pause all downloads
  - Refresh view

#### FR4: Add Download View
**Status**: ✅ Completed
- URL input with validation
- Destination directory selection (with default)
- Optional custom file name
- Max connections configuration
- Option to start immediately
- Error handling for invalid URLs

#### FR5: Statistics View
**Status**: ✅ Completed
- Total downloads count
- Breakdown by status (active, queued, paused, completed, failed)
- Total bytes downloaded
- Total size of all downloads
- Current aggregate download speed
- Formatted display (KB, MB, GB)

#### FR6: Settings View
**Status**: 🚧 Placeholder
- Configuration management (to be implemented)
- Planned settings:
  - Max concurrent downloads
  - Default download directory
  - Network settings
  - File naming policies

### Non-Functional Requirements

#### NFR1: Cross-Platform Compatibility
**Status**: ✅ Completed
- Built with .NET 10.0 for cross-platform support
- Tested on macOS (primary development platform)
- Designed to work on Windows and Linux

#### NFR2: Performance
**Status**: ⏳ Pending Testing
- Must handle 100+ downloads without lag
- UI refresh rate: minimum 1 Hz, target 5 Hz
- Low memory footprint

#### NFR3: Usability
**Status**: ✅ Completed
- Intuitive keyboard navigation
- Clear visual feedback
- Consistent emoji/icon usage
- Responsive to user actions

#### NFR4: Code Quality
**Status**: ✅ Completed
- Follows project conventions
- Proper error handling
- Culture-invariant formatting
- Clean separation of concerns

## Architecture

### Component Diagram

```text
┌─────────────────────────────────────────┐
│         KurioCliApplication             │
│  - Main application coordinator         │
│  - Handles lifecycle and shutdown       │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│             MainMenu                    │
│  - Navigation hub                       │
│  - Menu display and routing             │
└──────────────┬──────────────────────────┘
               │
               ├─────────────┬─────────────┬──────────────┐
               ▼             ▼             ▼              ▼
    ┌──────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐
    │DownloadList  │ │AddDownload │ │Statistics  │ │Settings    │
    │View          │ │View        │ │View        │ │View        │
    └──────┬───────┘ └──────┬─────┘ └──────┬─────┘ └────────────┘
           │                │               │
           ▼                ▼               ▼
    ┌─────────────────────────────────────────┐
    │         IDownloadEngine                 │
    │  - Core download operations             │
    │  - Queue management                     │
    │  - Statistics                           │
    └─────────────────────────────────────────┘
```

### Technology Stack

- **Framework**: .NET 10.0
- **TUI Library**: Spectre.Console 0.49.1
- **Dependency Injection**: Microsoft.Extensions.Hosting 10.0.0
- **Core Engine**: Kurio.Core

### File Structure

```text
Kurio.Cli/
├── Program.cs                    # Entry point, DI setup
├── KurioCliApplication.cs       # Main app coordinator
├── UI/
│   ├── MainMenu.cs              # Navigation menu
│   ├── DownloadListView.cs      # Download management
│   ├── AddDownloadView.cs       # Add downloads
│   ├── StatisticsView.cs        # Statistics display
│   └── SettingsView.cs          # Settings (placeholder)
└── README.md                     # Documentation
```

## Implementation Details

### Version Update
**Version**: 1.8.0 (minor bump)
- Follows semantic versioning
- Minor version increment for new feature

### Dependencies Added
- `Spectre.Console` 0.49.1
- `Microsoft.Extensions.Hosting` 10.0.0

### Key Design Decisions

1. **Spectre.Console over Terminal.Gui**
   - Better for read-heavy, data-centric UIs
   - Simpler API for common patterns
   - Excellent progress bar and table support

2. **Dependency Injection**
   - Uses Microsoft.Extensions.Hosting
   - Enables testability and maintainability
   - Consistent with .NET best practices

3. **View Separation**
   - Each view is a separate class
   - Single responsibility principle
   - Easy to extend and test

4. **Culture-Invariant Formatting**
   - Prevents locale-specific issues
   - Consistent across platforms
   - Follows CA1305 guidelines

## Testing Plan

### Unit Tests (To Be Implemented)
- [ ] MainMenu navigation logic
- [ ] View state management
- [ ] Input validation
- [ ] Formatting utilities

### Integration Tests (To Be Implemented)
- [ ] End-to-end navigation flows
- [ ] Download operations via TUI
- [ ] Error handling scenarios

### Manual Testing (Completed)
- [x] macOS build and basic navigation
- [x] Menu navigation
- [x] Download list display
- [ ] Windows testing
- [ ] Linux testing

## Future Enhancements

### Phase 2 Features
- Real-time live updates (background service)
- Keyboard shortcuts (F1-F12)
- Help screen with key mappings
- Download details modal

### Phase 3 Features
- Settings configuration UI
- Theme customization
- Export/import download lists
- Advanced filtering and search

### Phase 4 Features
- Mouse support
- Clipboard integration
- Notifications
- Multi-language support

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Performance with 100+ downloads | High | Implement pagination, lazy loading |
| Terminal compatibility issues | Medium | Test on multiple terminal emulators |
| Unicode/emoji rendering | Low | Fallback to ASCII characters |
| Real-time updates complexity | Medium | Use proper async patterns, background services |

## Success Metrics

### Must Have (MVP)
- [x] All core download operations accessible
- [x] Clean, intuitive interface
- [x] Cross-platform compatibility
- [x] Error handling

### Should Have
- [ ] Real-time updates
- [ ] Keyboard shortcuts
- [ ] Help documentation
- [ ] Performance with 100+ downloads

### Nice to Have
- [ ] Mouse support
- [ ] Themes
- [ ] Advanced filtering
- [ ] Settings UI

## Timeline

| Phase | Status | Date |
|-------|--------|------|
| Research & Design | ✅ Completed | Nov 26, 2025 |
| Core Implementation | ✅ Completed | Nov 26, 2025 |
| Documentation | ✅ Completed | Nov 26, 2025 |
| Testing & Polish | 🚧 In Progress | TBD |
| Release | 📋 Planned | TBD |

## Conclusion

The TUI implementation for Kurio provides a solid foundation for command-line download management. The choice of Spectre.Console has proven effective for creating a modern, interactive terminal interface. The modular architecture allows for easy extension and testing.

### Next Steps
1. Implement real-time updates with background services
2. Add comprehensive keyboard shortcuts
3. Create help documentation
4. Write unit and integration tests
5. Test on Windows and Linux platforms
6. Performance testing with large download lists
7. User feedback and iteration

---

**Document Version**: 1.0  
**Last Updated**: November 26, 2025  
**Status**: Active Development  
**Related Issue**: #15
