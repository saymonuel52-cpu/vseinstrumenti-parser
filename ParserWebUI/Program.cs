using Serilog;
using VseinstrumentiParser.Services;
using VseininstrumentiParser.WebUI.Components;
using VseininstrumentiParser.WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .WriteTo.Console()
        .WriteTo.File("logs/webui-.log", rollingInterval: RollingInterval.Day)
        .ReadFrom.Services(services);
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();

// Add HttpClient for API calls
builder.Services.AddHttpClient();

// Add parser services from main project
builder.Services.AddSingleton<VseinstrumentiParserService>();
builder.Services.AddSingleton<VoltCategoryParser>();
builder.Services.AddSingleton<VoltProductParser>();

// Add cache service (in-memory for now, can be Redis)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.CookieHttpOnly = true;
    options.CookieIsSecure = builder.Environment.IsProduction();
});

// Add authentication middleware
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Add monitoring and logging services
builder.Services.AddSingleton<ParserMonitoringService>();
builder.Services.AddSingleton<ParseProgressService>();

// Add API key authentication
builder.Services.AddSingleton<ApiKeyAuthenticationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// API Key middleware
app.UseMiddleware<ApiKeyMiddleware>();

app.UseSession();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// SignalR hubs
app.MapHub<ParseProgressHub>("/hubs/parse-progress");

// Health checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// API endpoints for web UI
app.MapPost("/api/parse/start", async (ParseRequest request, VseinstrumentiParserService parser) =>
{
    try
    {
        var result = await parser.StartParseAsync(request.Source, request.Category, request.Options);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapGet("/api/parse/status/{jobId}", async (string jobId, ParseProgressService progressService) =>
{
    var status = await progressService.GetJobStatusAsync(jobId);
    return status != null ? Results.Ok(status) : Results.NotFound();
});

app.MapGet("/api/metrics", async (ParserMonitoringService monitoring) =>
{
    var metrics = await monitoring.GetMetricsAsync();
    return Results.Ok(metrics);
});

app.MapGet("/api/logs/recent", async (int count, ILogger logger) =>
{
    // In production, integrate with Seq
    return Results.Ok(new { message = "Integrate with Seq API for real logs" });
});

Log.Information("Starting Vseinstrumenti Parser Web UI");

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
