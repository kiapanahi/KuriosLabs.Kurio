# Kurio Dashboard Deployment Guide

This guide covers deploying the Kurio web dashboard in various environments.

## Architecture

The Kurio dashboard consists of two components:

1. **Kurio.Server** - ASP.NET Core API with SignalR hubs
2. **Kurio.Web** - Blazor Server dashboard UI

Both can be deployed together or separately depending on your requirements.

## Deployment Options

### Option 1: Same-Origin Deployment (Recommended)

Host both services behind a reverse proxy under a single domain:

```
https://kurio.example.com/     → Kurio.Web
https://kurio.example.com/api/ → Kurio.Server
https://kurio.example.com/hubs/→ Kurio.Server (SignalR)
```

**Benefits:**
- No CORS configuration needed
- Simplified authentication
- Better security posture
- Single SSL certificate

**Configuration:**

See [Reverse Proxy Configuration](./reverse-proxy.md) for detailed setup.

### Option 2: Cross-Origin Deployment

Run services on different domains/ports:

```
https://dashboard.example.com → Kurio.Web
https://api.example.com       → Kurio.Server
```

**Benefits:**
- Independent scaling
- Separate infrastructure
- Isolated deployments

**Configuration:**

**Kurio.Server appsettings.json:**
```json
{
  "Kurio": {
    "Server": {
      "AllowedOrigins": [
        "https://dashboard.example.com"
      ],
      "CorsPolicy": "AllowWebClients"
    }
  }
}
```

**Kurio.Web appsettings.json:**
```json
{
  "KurioServer": {
    "BaseUrl": "https://api.example.com"
  }
}
```

## Configuration Reference

### Kurio.Server Settings

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `Kurio:Server:BaseUrl` | Public URL of the server | `https://localhost:7206` | Yes |
| `Kurio:Server:AllowedOrigins` | CORS allowed origins | `[]` | Yes (cross-origin) |
| `Kurio:Server:CorsPolicy` | CORS policy name | `AllowWebClients` | No |
| `Kurio:Server:Authentication:Enabled` | Enable authentication | `false` | No |
| `Kurio:Server:Authentication:Scheme` | Auth scheme (ApiKey/Bearer) | `ApiKey` | No |

### Kurio.Web Settings

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `KurioServer:BaseUrl` | Kurio.Server API URL | `https://localhost:7206` | Yes |
| `KurioServer:Hubs:Downloads` | Downloads hub path | `/hubs/downloads` | No |
| `KurioServer:Hubs:Queue` | Queue hub path | `/hubs/queue` | No |
| `KurioServer:Hubs:Stats` | Stats hub path | `/hubs/stats` | No |
| `KurioServer:Authentication:Enabled` | Enable API auth | `false` | No |
| `KurioServer:Authentication:ApiKey` | API key for server | `""` | If auth enabled |
| `Authentication:Enabled` | Enable UI auth | `false` | No |

## Authentication

### Disabling Authentication (Default)

For trusted internal networks or local development:

```json
{
  "Kurio": {
    "Server": {
      "Authentication": {
        "Enabled": false
      }
    }
  }
}
```

### Enabling API Key Authentication

For production deployments:

**1. Generate a secure API key:**

```bash
openssl rand -base64 32
```

**2. Configure Kurio.Server:**

```json
{
  "Kurio": {
    "Server": {
      "Authentication": {
        "Enabled": true,
        "Scheme": "ApiKey"
      }
    }
  }
}
```

**3. Add environment variable or secrets:**

```bash
export KURIO__SERVER__AUTHENTICATION__APIKEY="your-generated-api-key"
```

**4. Configure Kurio.Web:**

```json
{
  "KurioServer": {
    "Authentication": {
      "Enabled": true,
      "ApiKey": "your-generated-api-key"
    }
  }
}
```

**5. Enable UI authentication (optional):**

```json
{
  "Authentication": {
    "Enabled": true
  }
}
```

> **Note:** When UI authentication is enabled, users must authenticate to access the dashboard. Currently supports cookie-based authentication. External authentication providers (OAuth, OIDC) can be integrated as needed.

## Environment-Specific Configuration

### Development

Use `appsettings.Development.json` for local development:

```json
{
  "Kurio": {
    "Server": {
      "AllowedOrigins": [
        "http://localhost:5173",
        "http://localhost:3000",
        "https://localhost:5001"
      ]
    }
  }
}
```

### Staging

```json
{
  "Kurio": {
    "Server": {
      "BaseUrl": "https://staging.kurio.example.com",
      "AllowedOrigins": [
        "https://staging.kurio.example.com"
      ],
      "CorsPolicy": "SameOriginOnly"
    }
  }
}
```

### Production

```json
{
  "Kurio": {
    "Server": {
      "BaseUrl": "https://kurio.example.com",
      "AllowedOrigins": [
        "https://kurio.example.com"
      ],
      "CorsPolicy": "SameOriginOnly",
      "Authentication": {
        "Enabled": true,
        "Scheme": "ApiKey"
      }
    },
    "Storage": {
      "DefaultDestination": "/var/kurio/downloads",
      "TempDirectory": "/var/kurio/temp",
      "StateDirectory": "/var/kurio/state"
    }
  }
}
```

## Systemd Service Configuration

### Kurio.Server Service

Create `/etc/systemd/system/kurio-server.service`:

```ini
[Unit]
Description=Kurio Download Manager Server
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/kurio/server
ExecStart=/usr/bin/dotnet /opt/kurio/server/Kurio.Server.dll
Restart=always
RestartSec=10
User=kurio
Group=kurio
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:7206
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

### Kurio.Web Service

Create `/etc/systemd/system/kurio-web.service`:

```ini
[Unit]
Description=Kurio Download Manager Dashboard
After=network.target kurio-server.service
Requires=kurio-server.service

[Service]
Type=notify
WorkingDirectory=/opt/kurio/web
ExecStart=/usr/bin/dotnet /opt/kurio/web/Kurio.Web.dll
Restart=always
RestartSec=10
User=kurio
Group=kurio
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5001
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

### Enable and Start Services

```bash
sudo systemctl daemon-reload
sudo systemctl enable kurio-server kurio-web
sudo systemctl start kurio-server kurio-web
sudo systemctl status kurio-server kurio-web
```

## Docker Deployment

### Building Images

**Kurio.Server Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Kurio.Server/Kurio.Server.csproj", "src/Kurio.Server/"]
COPY ["src/Kurio.Core/Kurio.Core.csproj", "src/Kurio.Core/"]
COPY ["src/Kurio.Contracts/Kurio.Contracts.csproj", "src/Kurio.Contracts/"]
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
RUN dotnet restore "src/Kurio.Server/Kurio.Server.csproj"
COPY . .
WORKDIR "/src/src/Kurio.Server"
RUN dotnet build "Kurio.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kurio.Server.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 7206
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kurio.Server.dll"]
```

**Kurio.Web Dockerfile:**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Kurio.Web/Kurio.Web.csproj", "src/Kurio.Web/"]
COPY ["src/Kurio.Contracts/Kurio.Contracts.csproj", "src/Kurio.Contracts/"]
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
RUN dotnet restore "src/Kurio.Web/Kurio.Web.csproj"
COPY . .
WORKDIR "/src/src/Kurio.Web"
RUN dotnet build "Kurio.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kurio.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 5001
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kurio.Web.dll"]
```

### docker-compose.yml

See [Reverse Proxy Configuration](./reverse-proxy.md#docker-compose-deployment) for complete docker-compose setup.

## Health Monitoring

Both services expose health check endpoints:

- **Kurio.Server**: `https://kurio.example.com/health`
- **Kurio.Web**: Standard ASP.NET Core health checks

### Health Check Responses

**Healthy:**
```json
{
  "status": "Healthy",
  "checks": {
    "download_engine": "Healthy"
  }
}
```

**Unhealthy:**
```json
{
  "status": "Unhealthy",
  "checks": {
    "download_engine": "Unhealthy"
  }
}
```

## Performance Tuning

### Connection Limits

Configure maximum concurrent connections:

```json
{
  "Kurio": {
    "Engine": {
      "MaxConcurrentDownloads": 5,
      "DefaultMaxConnections": 8
    }
  }
}
```

### SignalR Settings

Tune SignalR for your load:

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false; // Disable in production
    options.MaximumReceiveMessageSize = 102400; // 100 KB
    options.StreamBufferCapacity = 10;
});
```

### Response Compression

Already enabled by default. Verify in production:

```bash
curl -H "Accept-Encoding: gzip" -I https://kurio.example.com/api/downloads
# Should include: Content-Encoding: gzip
```

## Logging

### Application Logs

Configure via `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.SignalR": "Information",
      "KuriousLabs.Kurio": "Information"
    }
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/kurio/server-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### Reverse Proxy Logs

Monitor reverse proxy logs for connection issues:

```bash
# Nginx
sudo tail -f /var/log/nginx/access.log /var/log/nginx/error.log

# Caddy
sudo tail -f /var/log/caddy/kurio.log
```

## Troubleshooting

### Dashboard Won't Load

1. Check Kurio.Server is running: `curl http://localhost:7206/health`
2. Check Kurio.Web is running: `curl http://localhost:5001`
3. Verify reverse proxy configuration
4. Check firewall rules
5. Review logs for errors

### SignalR Connection Fails

1. Verify WebSocket support: `curl -i -N -H "Connection: Upgrade" -H "Upgrade: websocket" https://kurio.example.com/hubs/downloads`
2. Check CORS configuration
3. Verify allowed origins include the UI origin
4. Test without authentication first
5. Review browser console for errors

### Downloads Not Starting

1. Check download engine health: `/health` endpoint
2. Verify storage paths are writable
3. Check download limits configuration
4. Review server logs for errors

## Security Checklist

- [ ] HTTPS enabled with valid certificate
- [ ] Authentication enabled in production
- [ ] CORS configured with specific origins (no wildcards)
- [ ] API keys stored securely (environment variables/secrets)
- [ ] Reverse proxy configured with security headers
- [ ] Firewall rules restrict direct access to backend services
- [ ] Logs configured without sensitive information
- [ ] Regular security updates applied
- [ ] Rate limiting configured at proxy level
- [ ] Health check endpoints don't expose sensitive data

## Support

For issues or questions:

- GitHub Issues: https://github.com/kiapanahi/KuriosLabs.Kurio/issues
- Documentation: https://github.com/kiapanahi/KuriosLabs.Kurio/docs

## See Also

- [Reverse Proxy Configuration](./reverse-proxy.md)
- [Configuration Reference](../configuration.md)
- [Troubleshooting Guide](../troubleshooting.md)
