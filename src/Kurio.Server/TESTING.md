# Kurio.Server Testing Guide

## Manual Testing Instructions

### 1. Start the Server

```bash
dotnet run --project src/Kurio.Server
```

The server will start on `http://localhost:5205` (check console output for actual port).

### 2. Test REST API Endpoints

#### Get All Downloads

```bash
curl http://localhost:5205/api/downloads | jq
```

#### Add a New Download

```bash
curl -X POST http://localhost:5205/api/downloads \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://speed.hetzner.de/100MB.bin",
    "fileName": "test-100mb.bin",
    "destinationDirectory": "/tmp/kurio-test"
  }' | jq
```

#### Get Specific Download

```bash
curl http://localhost:5205/api/downloads/{id} | jq
```

#### Start Download

```bash
curl -X POST http://localhost:5205/api/downloads/{id}/start | jq
```

#### Pause Download

```bash
curl -X POST http://localhost:5205/api/downloads/{id}/pause | jq
```

#### Resume Download

```bash
curl -X POST http://localhost:5205/api/downloads/{id}/resume | jq
```

#### Cancel Download

```bash
curl -X DELETE http://localhost:5205/api/downloads/{id}?removeFiles=false | jq
```

#### Change Priority

```bash
curl -X PATCH http://localhost:5205/api/downloads/{id}/priority \
  -H "Content-Type: application/json" \
  -d '{ "priority": "high" }' | jq
```

#### Pause All Downloads

```bash
curl -X POST http://localhost:5205/api/downloads/pause-all | jq
```

#### Clear Completed Downloads

```bash
curl -X DELETE http://localhost:5205/api/downloads/completed | jq
```

#### Get Statistics

```bash
curl http://localhost:5205/api/downloads/statistics | jq
```

### 3. Test Health Check

```bash
curl http://localhost:5205/health
```

Expected response:

```
Healthy
```

### 4. Test Swagger UI

Open browser: `http://localhost:5205/swagger`

This provides interactive API documentation and testing interface.

### 5. Test Server-Sent Events (SSE)

```bash
curl -N http://localhost:5205/api/downloads/stream
```

This will stream real-time download progress updates. Keep the connection open and start a download in another terminal
to see events.

### 6. Test SignalR Hub

SignalR requires a client library. You can test with a simple HTML page:

```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
</head>
<body>
    <h1>Kurio SignalR Test</h1>
    <div id="messages"></div>
    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("http://localhost:5205/hubs/downloads")
            .build();

        connection.on("DownloadProgress", (progress) => {
            console.log("Progress:", progress);
            document.getElementById("messages").innerHTML += 
                `<p>${JSON.stringify(progress)}</p>`;
        });

        connection.start()
            .then(() => {
                console.log("Connected to SignalR hub");
                return connection.invoke("SubscribeToProgress");
            })
            .catch(err => console.error(err));
    </script>
</body>
</html>
```

Save as `test-signalr.html` and open in browser.

## Expected Behavior

### Successful Responses

- **GET /api/downloads**: Returns array of downloads
- **POST /api/downloads**: Returns created download with ID
- **GET /api/downloads/{id}**: Returns download details
- **POST /api/downloads/{id}/start**: Returns 200 OK
- **POST /api/downloads/{id}/pause**: Returns 200 OK
- **POST /api/downloads/{id}/resume**: Returns 200 OK
- **DELETE /api/downloads/{id}**: Returns 204 No Content
- **PATCH /api/downloads/{id}/priority**: Returns 200 OK
- **POST /api/downloads/pause-all**: Returns 200 OK
- **DELETE /api/downloads/completed**: Returns 200 OK
- **GET /api/downloads/statistics**: Returns queue statistics
- **/health**: Returns "Healthy" status

### Error Responses

- **404 Not Found**: When download ID doesn't exist
- **400 Bad Request**: When invalid data provided (e.g., invalid URL)
- **500 Internal Server Error**: When unexpected error occurs

All error responses include:

```json
{
  "message": "Error description",
  "timestamp": "2025-01-26T19:00:00Z"
}
```

## Testing Checklist

- [ ] Server starts successfully
- [ ] Swagger UI accessible at /swagger
- [ ] Health check returns Healthy
- [ ] Can add new download
- [ ] Can list all downloads
- [ ] Can get specific download
- [ ] Can start download
- [ ] Can pause download
- [ ] Can resume download
- [ ] Can cancel download
- [ ] Can change priority
- [ ] Can pause all downloads
- [ ] Can clear completed downloads
- [ ] Statistics endpoint works
- [ ] SSE stream provides real-time updates
- [ ] SignalR hub receives progress events
- [ ] CORS allows configured origins
- [ ] Error handling returns proper responses
- [ ] Download progress updates in real-time
