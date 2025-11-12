using DocumentProcessor.Infrastructure;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Web.Components;
using DocumentProcessor.Application;
using DocumentProcessor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Blazor Server configuration with detailed error logging
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.DetailedErrors = true;
        }
    });

// Add infrastructure services (Database, Repositories, Document Sources)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add application services (Document processing, Background services)
builder.Services.AddApplicationServices();

// Add health checks
builder.Services.AddInfrastructureHealthChecks(builder.Configuration);

// Add additional logging with detailed error information
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);

    // Add detailed logging for Blazor
    logging.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Debug);
    logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
});

// Add rate limiting (simplified for now - can be enhanced later)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// Ensure database is created
await app.Services.EnsureDatabaseAsync();

// Re-queue stuck documents from previous runs
using (var scope = app.Services.CreateScope())
{
    var documentRepository = scope.ServiceProvider.GetRequiredService<DocumentProcessor.Core.Interfaces.IDocumentRepository>();
    var processingService = scope.ServiceProvider.GetRequiredService<DocumentProcessor.Application.Services.IDocumentProcessingService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Checking for stuck documents...");

    // Get all pending and queued documents
    var pendingDocs = await documentRepository.GetByStatusAsync(DocumentProcessor.Core.Entities.DocumentStatus.Pending);
    var queuedDocs = await documentRepository.GetByStatusAsync(DocumentProcessor.Core.Entities.DocumentStatus.Queued);
    var stuckDocuments = pendingDocs.Concat(queuedDocs).ToList();

    if (stuckDocuments.Any())
    {
        logger.LogInformation("Found {Count} stuck documents. Re-queuing them...", stuckDocuments.Count);

        foreach (var document in stuckDocuments)
        {
            try
            {
                logger.LogInformation("Re-queuing document {DocumentId}", document.Id);
                await processingService.QueueDocumentForProcessingAsync(document.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to re-queue document {DocumentId}", document.Id);
            }
        }

        logger.LogInformation("Finished re-queuing stuck documents");
    }
    else
    {
        logger.LogInformation("No stuck documents found in queue");
    }
}

// Configure the HTTP request pipeline.
var appLogger = app.Services.GetRequiredService<ILogger<Program>>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    appLogger.LogInformation("Running in Development mode with detailed error pages enabled");
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Add middleware to log all unhandled exceptions
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        appLogger.LogError(ex, "Unhandled exception occurred. Path: {Path}", context.Request.Path);
        throw;
    }
});

app.UseHttpsRedirection();

// Add response compression
app.UseResponseCompression();

// Add rate limiting
app.UseRateLimiter();

app.UseAntiforgery();

// Serve static files from wwwroot with explicit MIME type for scoped CSS
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".styles.css"] = "text/css";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});


// Serve files from the uploads directory
string uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");

// Ensure the uploads directory exists before creating the PhysicalFileProvider
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// Add cleanup endpoint for stuck documents
app.MapGet("/admin/cleanup-stuck-documents", async (IServiceProvider services) =>
{
    using var scope = services.CreateScope();
    var backgroundService = scope.ServiceProvider.GetRequiredService<DocumentProcessor.Application.Services.IBackgroundDocumentProcessingService>();
    
    await backgroundService.CleanupStuckDocumentsAsync(30); // 30 minutes timeout
    
    return Results.Ok(new { message = "Stuck documents cleanup initiated" });
});

app.Run();