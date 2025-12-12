# Reverse Proxy Configuration for Kurio Dashboard

This guide provides sample configurations for deploying Kurio with a reverse proxy, hosting both the dashboard UI (`Kurio.Web`) and API server (`Kurio.Server`) under a single domain.

## Overview

The recommended deployment architecture:
- **Kurio.Server** runs on `http://localhost:7206` (or configured port)
- **Kurio.Web** runs on `http://localhost:5001` (or configured port)
- **Reverse Proxy** (YARP/Nginx/Caddy) exposes both services at:
  - `https://example.com/` → Kurio.Web (dashboard)
  - `https://example.com/api/` → Kurio.Server (API endpoints)
  - `https://example.com/hubs/` → Kurio.Server (SignalR hubs)

This eliminates cross-origin concerns and simplifies authentication.

## YARP Configuration (ASP.NET Core)

YARP (Yet Another Reverse Proxy) is a .NET-based reverse proxy ideal for ASP.NET Core deployments.

### 1. Install YARP

```bash
dotnet new web -n Kurio.Gateway
cd Kurio.Gateway
dotnet add package Yarp.ReverseProxy
```

### 2. Configure appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Yarp": "Information"
    }
  },
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "kurio-server",
        "Match": {
          "Path": "/api/{**catch-all}"
        }
      },
      "hubs-route": {
        "ClusterId": "kurio-server",
        "Match": {
          "Path": "/hubs/{**catch-all}"
        }
      },
      "health-route": {
        "ClusterId": "kurio-server",
        "Match": {
          "Path": "/health"
        }
      },
      "ui-route": {
        "ClusterId": "kurio-web",
        "Match": {
          "Path": "{**catch-all}"
        }
      }
    },
    "Clusters": {
      "kurio-server": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:7206"
          }
        }
      },
      "kurio-web": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:5001"
          }
        }
      }
    }
  }
}
```

### 3. Configure Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.Run();
```

### 4. Run the Gateway

```bash
dotnet run --urls "https://localhost:443;http://localhost:80"
```

## Nginx Configuration

Nginx is a popular high-performance reverse proxy for production deployments.

### nginx.conf

```nginx
http {
    upstream kurio_server {
        server localhost:7206;
    }

    upstream kurio_web {
        server localhost:5001;
    }

    # WebSocket and SignalR configuration
    map $http_upgrade $connection_upgrade {
        default upgrade;
        '' close;
    }

    server {
        listen 80;
        server_name kurio.example.com;
        return 301 https://$server_name$request_uri;
    }

    server {
        listen 443 ssl http2;
        server_name kurio.example.com;

        ssl_certificate /etc/ssl/certs/kurio.example.com.crt;
        ssl_certificate_key /etc/ssl/private/kurio.example.com.key;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers HIGH:!aNULL:!MD5;

        # API and SignalR backend
        location /api/ {
            proxy_pass http://kurio_server;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_cache_bypass $http_upgrade;
            
            # Timeouts for long-running operations
            proxy_read_timeout 600s;
            proxy_send_timeout 600s;
        }

        # SignalR hubs
        location /hubs/ {
            proxy_pass http://kurio_server;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_cache_bypass $http_upgrade;
            
            # SignalR requires longer timeouts
            proxy_read_timeout 86400s;
            proxy_send_timeout 86400s;
        }

        # Health check endpoint
        location /health {
            proxy_pass http://kurio_server;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # Dashboard UI
        location / {
            proxy_pass http://kurio_web;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_cache_bypass $http_upgrade;
        }

        # Blazor SignalR endpoint
        location /_blazor {
            proxy_pass http://kurio_web;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;
            proxy_set_header Host $host;
            proxy_cache_bypass $http_upgrade;
        }
    }
}
```

### Test and Reload Nginx

```bash
sudo nginx -t
sudo systemctl reload nginx
```

## Caddy Configuration

Caddy provides automatic HTTPS with Let's Encrypt and simple configuration.

### Caddyfile

```caddy
kurio.example.com {
    # Enable automatic HTTPS
    tls admin@example.com

    # API and SignalR backend
    handle /api/* {
        reverse_proxy localhost:7206 {
            header_up X-Real-IP {remote_host}
            header_up X-Forwarded-For {remote_host}
            header_up X-Forwarded-Proto {scheme}
            
            # Long timeouts for downloads
            transport http {
                read_timeout 600s
                write_timeout 600s
            }
        }
    }

    # SignalR hubs
    handle /hubs/* {
        reverse_proxy localhost:7206 {
            header_up X-Real-IP {remote_host}
            header_up X-Forwarded-For {remote_host}
            header_up X-Forwarded-Proto {scheme}
            
            # Very long timeout for persistent connections
            transport http {
                read_timeout 86400s
                write_timeout 86400s
            }
        }
    }

    # Health check
    handle /health {
        reverse_proxy localhost:7206
    }

    # Dashboard UI (default handler)
    handle {
        reverse_proxy localhost:5001 {
            header_up X-Real-IP {remote_host}
            header_up X-Forwarded-For {remote_host}
            header_up X-Forwarded-Proto {scheme}
        }
    }

    # Enable compression
    encode gzip zstd

    # Logging
    log {
        output file /var/log/caddy/kurio.log
        format json
    }
}
```

### Run Caddy

```bash
caddy run --config Caddyfile
# Or as a service
sudo systemctl start caddy
```

## Kurio Configuration for Reverse Proxy

When running behind a reverse proxy, update application settings:

### Kurio.Server appsettings.json

```json
{
  "Kurio": {
    "Server": {
      "BaseUrl": "https://kurio.example.com",
      "AllowedOrigins": [
        "https://kurio.example.com"
      ],
      "CorsPolicy": "SameOriginOnly"
    }
  }
}
```

### Kurio.Web appsettings.json

```json
{
  "KurioServer": {
    "BaseUrl": "https://kurio.example.com",
    "Hubs": {
      "Downloads": "/hubs/downloads",
      "Queue": "/hubs/queue",
      "Stats": "/hubs/stats"
    }
  }
}
```

## Local Development Without Reverse Proxy

For local development, run services independently:

```bash
# Terminal 1: Start Kurio.Server
cd src/Kurio.Server
dotnet run

# Terminal 2: Start Kurio.Web
cd src/Kurio.Web
dotnet run

# Access dashboard at https://localhost:5001
```

Use the `appsettings.Development.json` with permissive CORS:

```json
{
  "Kurio": {
    "Server": {
      "AllowedOrigins": [
        "http://localhost:5173",
        "http://localhost:3000",
        "https://localhost:5001"
      ],
      "CorsPolicy": "AllowWebClients"
    }
  }
}
```

## Docker Compose Deployment

Sample `docker-compose.yml` for containerized deployment:

```yaml
version: '3.8'

services:
  kurio-server:
    image: kurio/server:latest
    container_name: kurio-server
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:7206
    volumes:
      - ./downloads:/app/downloads
      - ./config/server-appsettings.json:/app/appsettings.Production.json:ro
    networks:
      - kurio-net

  kurio-web:
    image: kurio/web:latest
    container_name: kurio-web
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5001
    volumes:
      - ./config/web-appsettings.json:/app/appsettings.Production.json:ro
    networks:
      - kurio-net

  nginx:
    image: nginx:alpine
    container_name: kurio-nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/ssl:ro
    depends_on:
      - kurio-server
      - kurio-web
    networks:
      - kurio-net

networks:
  kurio-net:
    driver: bridge
```

## Security Considerations

1. **HTTPS Only**: Always use HTTPS in production
2. **Authentication**: Enable authentication in production environments
3. **CORS**: Use strict same-origin policy when behind reverse proxy
4. **Headers**: Ensure `X-Forwarded-*` headers are set correctly
5. **Timeouts**: Configure appropriate timeouts for long-running downloads
6. **Rate Limiting**: Consider adding rate limiting at the proxy level

## Troubleshooting

### SignalR Connection Fails

- Verify WebSocket upgrade headers are configured
- Check firewall allows WebSocket connections
- Ensure timeouts are sufficient for long-lived connections
- Validate CORS configuration matches frontend origin

### Authentication Issues

- Confirm API keys match between Web and Server
- Check cookie settings if using cookie authentication
- Verify HTTPS is used for secure cookies
- Test with authentication disabled first

### Performance Issues

- Enable compression at proxy level
- Configure appropriate connection pooling
- Monitor backend service health
- Consider CDN for static assets

## References

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Nginx SignalR Configuration](https://docs.nginx.com/nginx/admin-guide/web-server/reverse-proxy/)
- [Caddy Reverse Proxy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy)
- [ASP.NET Core SignalR with Proxies](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale)
