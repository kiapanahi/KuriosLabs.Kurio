# Kurio.Server

ASP.NET Core web service that hosts the Kurio download engine and exposes it via REST API and real-time communication
channels (SignalR/SSE).

## Overview

Kurio.Server is the backend component of the Kurio download manager that provides:

- **REST API** for all download operations (CRUD, queue management)
- **SignalR Hub** for bidirectional real-time communication
- **Server-Sent Events (SSE)** for simple server-to-client progress streaming
- **Background Services** for hosting the download engine and broadcasting progress

## Features

- ✅ Full REST API for download management
- ✅ Real-time progress updates via SignalR
- ✅ SSE endpoint for simple clients
- ✅ OpenAPI/Swagger documentation
- ✅ Health checks for monitoring
- ✅ CORS support for web clients
- ✅ Comprehensive error handling
- ✅ Graceful shutdown (pauses active downloads)

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- macOS, Linux, or Windows

### Running the Server

```bash
cd src/Kurio.Server
dotnet run
```

The server will start on:

- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

In development mode, Swagger UI is available at: `http://localhost:5000`

### Configuration

Configuration is managed through `appsettings.json`. Key settings:

```json
{
  "Kurio": {
    "Server": {
      "AllowedOrigins": ["http://localhost:5173"],
      "EnableSwagger": true
    },
    "Storage": {
      "DefaultDestination": "~/Downloads",
      "TempDirectory": "~/Downloads/.kurio/temp",
      "Mode": "SingleFile",
      "VerifyWrites": true
    },
    "Engine": {
      "MaxConcurrentDownloads": 3,
      "DefaultMaxConnections": 8
    }
  }
}
```

## API Endpoints

### Downloads Management

- `POST /api/downloads` - Add new download
- `GET /api/downloads` - Get all downloads (with optional filtering)
- `GET /api/downloads/{id}` - Get specific download
- `POST /api/downloads/{id}/start` - Start a queued download
- `POST /api/downloads/{id}/pause` - Pause an active download
- `POST /api/downloads/{id}/resume` - Resume a paused download
- `DELETE /api/downloads/{id}` - Cancel a download
- `POST /api/downloads/{id}/priority` - Change download priority
- `POST /api/downloads/pause-all` - Pause all active downloads
- `POST /api/downloads/clear-completed` - Clear completed downloads
- `GET /api/downloads/statistics` - Get queue statistics

### Real-Time Communication

- `SignalR Hub: /hubs/downloads` - Bidirectional real-time updates
- `SSE Endpoint: /api/downloads/stream` - Server-to-client progress streaming

### Monitoring

- `GET /health` - Health check endpoint

## API Examples

### Add a Download

```bash
curl -X POST http://localhost:5000/api/downloads \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://example.com/file.zip",
    "fileName": "myfile.zip",
    "destinationDirectory": "~/Downloads",
    "maxConnections": 8,
    "priority": "Normal"
  }'
```

### Get All Downloads

```bash
curl http://localhost:5000/api/downloads
```

### Get Queue Statistics

```bash
curl http://localhost:5000/api/downloads/statistics
```

### Pause a Download

```bash
curl -X POST http://localhost:5000/api/downloads/{id}/pause
```

## SignalR Client Example

### JavaScript/TypeScript

```typescript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5000/hubs/downloads')
    .withAutomaticReconnect()
    .build();

connection.on('ProgressUpdate', (progress) => {
    console.log('Progress:', progress);
    // Update UI with progress
});

await connection.start();
await connection.invoke('SubscribeToProgress', null); // null = all downloads
```

## SSE Client Example

### JavaScript

```javascript
const eventSource = new EventSource('http://localhost:5000/api/downloads/stream');

eventSource.addEventListener('progress', (event) => {
    const progress = JSON.parse(event.data);
    console.log('Progress:', progress);
    // Update UI with progress
});

eventSource.onerror = (error) => {
    console.error('SSE error:', error);
};
```

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│            Kurio.Server (ASP.NET Core)                  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         REST API Layer (Controllers)              │  │
│  │  - CRUD operations for downloads                  │  │
│  │  - Queue management endpoints                     │  │
│  │  - Statistics and monitoring                      │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         Real-time Layer                           │  │
│  │  - SignalR Hub (bidirectional)                    │  │
│  │  - SSE Endpoint (server-to-client)                │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         Background Services                       │  │
│  │  - DownloadEngineHostedService                    │  │
│  │  - ProgressBroadcaster                            │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         Kurio.Core (Engine)                       │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Docker

(Coming soon)

## License

MIT License. See LICENSE file for details.
