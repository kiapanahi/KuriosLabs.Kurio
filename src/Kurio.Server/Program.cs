using System.Text.Json;
using System.Text.Json.Serialization;

using KuriousLabs.Kurio;
using KuriousLabs.Kurio.Core;
using KuriousLabs.Kurio.Server.Endpoints;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Services;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// JSON settings for minimal-API request/response bodies (Http.Json.JsonOptions - the minimal-API
// counterpart of the MVC AddJsonOptions configuration this API used before the controller removal).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Surface parameter/body binding failures as a 400 in every environment. Minimal APIs default
// ThrowOnBadRequest to true in Development, which would turn what MVC answered with a 400 into a
// 500 via UseExceptionHandler.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.Services.AddOpenApi()
    .AddEndpointsApiExplorer();

// Add problem details service for standardized error responses
builder.Services.AddProblemDetails();

// Authorization services (previously pulled in transitively by AddControllers)
builder.Services.AddAuthorization();

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100 KB
});

// Add Kurio.Core services
builder.Services.AddKurioConfiguration();
builder.Services.AddKurioDownloadEngine();

// Add hosted services
builder.Services.AddHostedService<DownloadEngineHostedService>();
builder.Services.AddSingleton<ProgressBroadcaster>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProgressBroadcaster>());
builder.Services.AddSingleton<StatsBroadcaster>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StatsBroadcaster>());

// Add CORS with enhanced configuration for dashboard and third-party clients
var allowedOrigins = builder.Configuration
    .GetSection("Kurio:Server:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173", "http://localhost:3000"];

var corsPolicy = builder.Configuration.GetValue<string>("Kurio:Server:CorsPolicy") ?? "AllowWebClients";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowedToAllowWildcardSubdomains();
    });

    // Stricter policy for production reverse-proxy scenarios
    options.AddPolicy("SameOriginOnly", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetValue<string>("Kurio:Server:BaseUrl") ?? "https://localhost:7206")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add response compression
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<DownloadEngineHealthCheck>("download_engine");

var app = builder.Build();

// Configure pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCors(corsPolicy);
app.UseAuthorization();

app.MapDownloadEndpoints();
app.MapQueueEndpoints();
app.MapStatsEndpoints();
app.MapConfigurationEndpoints();
app.MapProgressStreamEndpoints();

app.MapHub<DownloadHub>("/hubs/downloads");
app.MapHub<QueueHub>("/hubs/queue");
app.MapHub<StatsHub>("/hubs/stats");
app.MapHealthChecks("/health");

app.Run();
