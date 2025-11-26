using Kurio.Core;
using Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Middleware;
using KuriousLabs.Kurio.Server.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Add Swagger without OpenAPI source generators
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100 KB
});

// Add Kurio.Core services
builder.Services.AddKurioDownloadEngine();

// Add hosted services
builder.Services.AddHostedService<DownloadEngineHostedService>();
builder.Services.AddSingleton<ProgressBroadcaster>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProgressBroadcaster>());

// Add CORS
var allowedOrigins = builder.Configuration
    .GetSection("Kurio:Server:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173", "http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebClients", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<DownloadEngineHealthCheck>("download_engine");

var app = builder.Build();

// Configure pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Kurio API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthorization();

app.MapControllers();
app.MapHub<DownloadHub>("/hubs/downloads");
app.MapHealthChecks("/health");

// SSE endpoint for progress streaming
app.MapGet("/api/downloads/stream", async (
    IDownloadEngine engine,
    Guid? taskId,
    CancellationToken cancellationToken) =>
{
    return Results.Stream(async stream =>
    {
        await using var writer = new StreamWriter(stream) { AutoFlush = true };

        await writer.WriteLineAsync("retry: 10000\n");

        await foreach (var progress in engine.StreamProgressAsync(taskId, cancellationToken))
        {
            var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });

            await writer.WriteLineAsync($"event: progress");
            await writer.WriteLineAsync($"data: {json}");
            await writer.WriteLineAsync();
        }
    }, "text/event-stream");
})
.WithName("StreamProgress")
.WithTags("Progress")
.Produces(200, contentType: "text/event-stream");

app.Run();
