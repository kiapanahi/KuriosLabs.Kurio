using System.Linq;
using KuriousLabs.Kurio.Web.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.AddOptions<KurioServerOptions>()
    .Bind(builder.Configuration.GetSection("KurioServer"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<KurioServerOptions>, KurioServerOptionsValidator>();

// Services
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
});
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddHttpClient<KurioApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<KurioServerOptions>>().CurrentValue;
    client.BaseAddress = options.BaseUrl;
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
}).AddStandardResilienceHandler();

builder.Services.AddSingleton<HubClientFactory>();
builder.Services.AddSingleton<ConnectionStateService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ConnectionStateService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseResponseCompression();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
