using DocumentProcessor.Infrastructure;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Web.Components;
using DocumentProcessor.Web.Hubs;
using DocumentProcessor.Web.Services;
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

// Add SignalR
builder.Services.AddSignalR();

// Add notification service
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add infrastructure services (Database, Repositories, Document Sources)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add application services (Document processing, Background services)
builder.Services.AddApplicationServices();

// Add health checks
builder.Services.AddInfrastructureHealthChecks(builder.Configuration);

// Add additional logging if needed
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
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

// Ensure database is created and migrations are applied
await app.Services.EnsureDatabaseAsync();

// Initialize stored procedures
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DocumentProcessor.Infrastructure.Data.StoredProcedureInitializer.InitializeStoredProceduresAsync(context, logger);
}

// Seed test document types if none exist
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    if (!context.DocumentTypes.Any(dt => dt.IsActive))
    {
        logger.LogInformation("Seeding test document types...");
        
        var documentTypes = new[]
        {
            new DocumentType
            {
                Id = Guid.NewGuid(),
                Name = "PDF Documents",
                Description = "Portable Document Format files",
                Category = "General",
                IsActive = true,
                Priority = 1,
                FileExtensions = ".pdf",
                Keywords = "pdf,document,portable",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new DocumentType
            {
                Id = Guid.NewGuid(),
                Name = "Word Documents",
                Description = "Microsoft Word documents",
                Category = "General",
                IsActive = true,
                Priority = 2,
                FileExtensions = ".docx,.doc",
                Keywords = "word,document,microsoft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new DocumentType
            {
                Id = Guid.NewGuid(),
                Name = "Excel Spreadsheets",
                Description = "Microsoft Excel spreadsheet files",
                Category = "Data",
                IsActive = true,
                Priority = 3,
                FileExtensions = ".xlsx,.xls",
                Keywords = "excel,spreadsheet,data",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        
        context.DocumentTypes.AddRange(documentTypes);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Test document types seeded successfully");
    }
}

// Re-queue stuck documents from previous runs
using (var scope = app.Services.CreateScope())
{
    var processingService = scope.ServiceProvider.GetRequiredService<DocumentProcessor.Application.Services.IDocumentProcessingService>();
    var processingQueueRepo = scope.ServiceProvider.GetService<DocumentProcessor.Core.Interfaces.IProcessingQueueRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (processingQueueRepo != null)
    {
        logger.LogInformation("Checking for stuck documents in queue...");

        // Get all pending queue items
        var stuckItems = await processingQueueRepo.GetByStatusAsync(DocumentProcessor.Core.Entities.ProcessingStatus.Pending);
        var stuckItemsList = stuckItems.ToList();

        if (stuckItemsList.Any())
        {
            logger.LogInformation("Found {Count} stuck documents in queue. Re-queuing them...", stuckItemsList.Count);

            foreach (var item in stuckItemsList)
            {
                try
                {
                    logger.LogInformation("Re-queuing document {DocumentId} from queue item {QueueId}", item.DocumentId, item.Id);
                    await processingService.QueueDocumentForProcessingAsync(item.DocumentId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to re-queue document {DocumentId}", item.DocumentId);
                }
            }

            logger.LogInformation("Finished re-queuing stuck documents");
        }
        else
        {
            logger.LogInformation("No stuck documents found in queue");
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Development environment configuration
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Add response compression
app.UseResponseCompression();

// Add rate limiting
app.UseRateLimiter();

app.UseAntiforgery();

// Serve static files from wwwroot
app.UseStaticFiles();


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

// Map SignalR hub
app.MapHub<DocumentProcessingHub>("/documentHub");

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