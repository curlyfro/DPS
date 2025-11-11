using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.AI;
using DocumentProcessor.Infrastructure.BackgroundTasks;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Infrastructure.Providers;
using DocumentProcessor.Infrastructure.Repositories;
using DocumentProcessor.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Entity Framework
        // Use local connection string if available, otherwise use AWS Secrets Manager
        var localConnectionString = configuration.GetConnectionString("LocalSqlite");
        if (!string.IsNullOrEmpty(localConnectionString))
        {
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(localConnectionString));
        }
        else
        {
            // Build connection string from AWS Secrets Manager
            var connectionString = BuildConnectionStringFromSecretsManager().GetAwaiter().GetResult();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        }

        // Register repositories
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register document source providers
        services.AddSingleton<LocalFileSystemProvider>();
        services.AddSingleton<IDocumentSourceFactory, DocumentSourceFactory>();
        services.AddScoped<IDocumentSourceProvider>(provider =>
        {
            var factory = provider.GetRequiredService<IDocumentSourceFactory>();
            var sourceType = configuration.GetValue<string>("DocumentStorage:Provider") ?? "LocalFileSystem";
            return factory.CreateProvider(sourceType);
        });

        // Register AI processing services
        // Change to Scoped to support scoped dependencies
        services.AddScoped<IAIProcessorFactory, AIProcessorFactory>();
        
        // Register DocumentContentExtractor
        services.AddScoped<DocumentContentExtractor>();

        // Register Bedrock configuration
        var bedrockSection = configuration.GetSection("Bedrock");
        services.Configure<BedrockOptions>(options =>
        {
            bedrockSection.Bind(options);
        });
            
        // Register background task services
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            
        // Register hosted services
        var maxConcurrency = configuration.GetValue<int>("BackgroundTasks:MaxConcurrency", 3);

        // Register the DocumentProcessingHostedService
        services.AddHostedService(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DocumentProcessingHostedService>>();
            var queue = provider.GetRequiredService<IBackgroundTaskQueue>();
            logger.LogInformation("Creating DocumentProcessingHostedService with max concurrency: {MaxConcurrency}", maxConcurrency);
            return new DocumentProcessingHostedService(queue, logger, maxConcurrency);
        });

        // Note: IDocumentProcessingService is registered in the Application layer

        return services;
    }

    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database")
            .AddCheck("document-storage", () =>
            {
                // Simple health check for document storage
                var provider = configuration.GetValue<string>("DocumentStorage:Provider");
                if (string.IsNullOrEmpty(provider))
                {
                    return HealthCheckResult.Unhealthy("No document storage provider configured");
                }
                return HealthCheckResult.Healthy($"Document storage provider: {provider}");
            });

        return services;
    }

    public static async Task EnsureDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
        var hostEnvironment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        try
        {
            logger.LogInformation("Ensuring database exists...");

            // In development, drop and recreate the database to ensure schema is up to date
            // This is safe for development but should NEVER be done in production
            if (hostEnvironment.IsDevelopment())
            {
                logger.LogWarning("Development mode: Dropping and recreating database to ensure schema is current");
                await context.Database.EnsureDeletedAsync();
                logger.LogInformation("Database dropped successfully");
            }

            // Create database from model without running migrations
            // This will create the database with all tables, indexes, and relationships
            // based on the current DbContext model
            var created = await context.Database.EnsureCreatedAsync();

            if (created)
            {
                logger.LogInformation("Database created successfully from model");

                // Create database view for document summaries
                logger.LogInformation("Creating database view: vw_DocumentSummary");
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE VIEW vw_DocumentSummary AS
                    SELECT
                        DocumentTypeName,
                        Status,
                        COUNT(*) AS DocumentCount,
                        AVG(DATEDIFF(SECOND, UploadedAt, COALESCE(ProcessedAt, GETUTCDATE()))) AS AvgProcessingTimeSeconds,
                        MIN(UploadedAt) AS FirstUploadedAt,
                        MAX(UploadedAt) AS LastUploadedAt
                    FROM Documents
                    WHERE IsDeleted = 0
                    GROUP BY DocumentTypeName, Status
                ");
                logger.LogInformation("Created view: vw_DocumentSummary");

                // Create stored procedure for getting recent documents
                logger.LogInformation("Creating stored procedure: sp_GetRecentDocuments");
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE PROCEDURE sp_GetRecentDocuments
                        @Days INT = 7,
                        @Status INT = NULL,
                        @DocumentTypeName NVARCHAR(200) = NULL
                    AS
                    BEGIN
                        SET NOCOUNT ON;

                        SELECT
                            Id,
                            FileName,
                            FileExtension,
                            StoragePath,
                            FileSize,
                            DocumentTypeName,
                            DocumentTypeCategory,
                            Status,
                            ProcessingStatus,
                            Summary,
                            UploadedAt,
                            ProcessedAt,
                            ProcessingStartedAt,
                            ProcessingCompletedAt
                        FROM Documents
                        WHERE IsDeleted = 0
                            AND UploadedAt >= DATEADD(DAY, -@Days, GETUTCDATE())
                            AND (@Status IS NULL OR Status = @Status)
                            AND (@DocumentTypeName IS NULL OR DocumentTypeName = @DocumentTypeName)
                        ORDER BY UploadedAt DESC
                    END
                ");
                logger.LogInformation("Created stored procedure: sp_GetRecentDocuments");
            }
            else
            {
                logger.LogInformation("Database already exists");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while ensuring database exists");
            throw;
        }
    }

    /// <summary>
    /// Builds a SQL Server connection string from AWS Secrets Manager
    /// </summary>
    private static async Task<string> BuildConnectionStringFromSecretsManager()
    {
        var secretsService = new SecretsManagerService();

        // Get credentials from secret with description starting with "Password for RDS MSSQL used for MAM319."
        var credentialsSecretJson = await secretsService.GetSecretByDescriptionPrefixAsync("Password for RDS MSSQL used for MAM319.");
        var username = secretsService.GetFieldFromSecret(credentialsSecretJson, "username");
        var password = secretsService.GetFieldFromSecret(credentialsSecretJson, "password");

        // Get connection info from secret named "atx-db-modernization-1"
        var connectionInfoSecretJson = await secretsService.GetSecretAsync("atx-db-modernization-1");
        var host = secretsService.GetFieldFromSecret(connectionInfoSecretJson, "host");
        var port = secretsService.GetFieldFromSecret(connectionInfoSecretJson, "port");
        var dbname = secretsService.GetFieldFromSecret(connectionInfoSecretJson, "dbname");

        // Build SQL Server connection string
        var connectionString = $"Server={host},{port};Database={dbname};User Id={username};Password={password};TrustServerCertificate=true;MultipleActiveResultSets=true";

        return connectionString;
    }
}