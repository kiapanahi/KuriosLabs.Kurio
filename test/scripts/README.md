# Kurio Test Scripts

This directory contains test scripts for manual and automated testing of Kurio download manager functionality.

## Prerequisites

Before running any test scripts, ensure you have:

1. **jq** installed (for JSON parsing):

   ```bash
   brew install jq
   ```

2. **curl** installed (usually pre-installed on macOS/Linux)

3. **Kurio Server running**:

   ```bash
   dotnet run --project src/Kurio.Server
   ```

## Available Scripts

### test-pause-resume.sh

Tests the pause and resume functionality of the download manager.

#### What it does

1. Creates a download task with configurable parameters
2. Waits for a specified duration to allow download progress
3. Pauses the download
4. Reads the `bytesDownloaded` from the state file (`~/.kurio/state/<download_id>.json`)
5. Prompts the user to resume the download
6. Resumes the download

#### Usage

Basic usage with defaults:

```bash
./test/scripts/test-pause-resume.sh
```

#### Configuration

The script supports environment variables for customization:

| Variable | Default | Description |
|----------|---------|-------------|
| `API_BASE_URL` | `http://localhost:5205` | Base URL of the Kurio API server |
| `DOWNLOAD_URL` | `https://speed.hetzner.de/100MB.bin` | URL to download from |
| `DOWNLOAD_FILENAME` | `test-pause-resume.bin` | Name for the downloaded file |
| `DESTINATION_DIR` | `/tmp/kurio-test` | Where to save the download |
| `MAX_CONNECTIONS` | `8` | Number of concurrent connections |
| `PRIORITY` | `Normal` | Download priority (Low, Normal, High, Critical) |
| `WAIT_SECONDS` | `5` | Seconds to wait before pausing |
| `STATE_DIR` | `$HOME/.kurio/state` | Directory containing state files |

#### Examples

Test with a different file:

```bash
DOWNLOAD_URL="https://releases.ubuntu.com/22.04/ubuntu-22.04.3-desktop-amd64.iso" \
DOWNLOAD_FILENAME="ubuntu.iso" \
WAIT_SECONDS=10 \
./test/scripts/test-pause-resume.sh
```

Test with fewer connections and shorter wait:

```bash
MAX_CONNECTIONS=4 \
WAIT_SECONDS=3 \
./test/scripts/test-pause-resume.sh
```

Test with different server URL:

```bash
API_BASE_URL="http://localhost:8080" \
./test/scripts/test-pause-resume.sh
```

#### Output

The script provides colored, step-by-step output:

- **Cyan [INFO]**: Information messages
- **Green [SUCCESS]**: Successful operations
- **Yellow [WARNING]**: Warnings (non-fatal)
- **Red [ERROR]**: Errors (fatal)
- **Blue ==>**: Major steps

Example output:

```text
==> Kurio Download Manager - Pause/Resume Test

[INFO] Configuration:
  API URL:         http://localhost:5205/api/downloads
  Download URL:    https://speed.hetzner.de/100MB.bin
  Filename:        test-pause-resume.bin
  Destination:     /tmp/kurio-test
  Connections:     8
  Priority:        Normal
  Wait Duration:   5s
  State Directory: /Users/user/.kurio/state

==> Step 1: Creating download task

[SUCCESS] Download created with ID: 12345678-90ab-cdef-1234-567890abcdef

==> Step 2: Waiting for 5 seconds to allow download progress

  ✓ Wait complete

==> Step 3: Pausing download

[SUCCESS] Download paused successfully

==> Step 4: Reading download progress from state file

[SUCCESS] State file found at: /Users/user/.kurio/state/12345678-90ab-cdef-1234-567890abcdef.json
[SUCCESS] Bytes downloaded (from state file): 524288

==> Step 5: Resume download

Press Enter to resume the download...

[SUCCESS] Download resumed successfully

==> Test Complete

[SUCCESS] Pause/Resume test completed successfully!
```

#### Troubleshooting

**Server not responding:**

```text
[ERROR] Server is not responding at http://localhost:5205
[ERROR] Please start the server: dotnet run --project src/Kurio.Server
```

→ Start the Kurio server before running the script

**State file not found:**

```text
[WARNING] State file not found at /Users/user/.kurio/state/<id>.json
```

→ The script will fall back to using the API to retrieve progress information

**jq not installed:**

```text
[ERROR] jq is not installed (brew install jq)
```

→ Install jq: `brew install jq`

## Adding New Test Scripts

When creating new test scripts:

1. Use bash with `set -euo pipefail` for safety
2. Include clear documentation in comments
3. Use environment variables for configuration
4. Add colored output for better UX
5. Include dependency checks
6. Make the script executable: `chmod +x script-name.sh`
7. Document in this README

## See Also

- [Kurio.Server Testing Guide](../../src/Kurio.Server/TESTING.md) - Manual API testing
- [Kurio.Server README](../../src/Kurio.Server/README.md) - API documentation
- [Implementation Plan](../../docs/implementation-plan.md) - Project roadmap
