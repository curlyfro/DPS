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
        // Add Entity Framework - Always use PostgreSQL (RDS)
        // Default: Use AWS Secrets Manager
        // Fallback: Use local connection string from configuration
        string connectionString;

        try
        {
            // Primary: Build connection string from AWS Secrets Manager
            connectionString = BuildConnectionStringFromSecretsManager().GetAwaiter().GetResult();
        }
        catch
        {
            // Fallback: Use local connection string if Secrets Manager is unavailable
            var localConnectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(localConnectionString))
            {
                throw new InvalidOperationException("Unable to retrieve connection string from AWS Secrets Manager and no local connection string is configured.");
            }
            connectionString = localConnectionString;
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

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

            // Always drop and recreate the database to ensure schema is up to date
            logger.LogWarning("Dropping and recreating database to ensure schema is current");
            await context.Database.EnsureDeletedAsync();
            logger.LogInformation("Database dropped successfully");

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
                    CREATE VIEW dps_dbo.vw_DocumentSummary AS
                    SELECT
                        documenttypename,
                        status,
                        COUNT(*) AS DocumentCount,
                        AVG(EXTRACT(EPOCH FROM (COALESCE(processedat, NOW() AT TIME ZONE 'UTC') - uploadedat))) AS AvgProcessingTimeSeconds,
                        MIN(uploadedat) AS FirstUploadedAt,
                        MAX(uploadedat) AS LastUploadedAt
                    FROM dps_dbo.documents
                    WHERE isdeleted = 0
                    GROUP BY documenttypename, status
                ");
                logger.LogInformation("Created view: vw_DocumentSummary");

                // Create stored procedure (function) for getting recent documents
                logger.LogInformation("Creating stored function: sp_GetRecentDocuments");
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE OR REPLACE FUNCTION dps_dbo.sp_GetRecentDocuments(
                        p_Days INT DEFAULT 7,
                        p_Status INT DEFAULT NULL,
                        p_DocumentTypeName VARCHAR(200) DEFAULT NULL
                    )
                    RETURNS TABLE (
                        Id UUID,
                        FileName VARCHAR(500),
                        FileExtension VARCHAR(50),
                        StoragePath VARCHAR(1000),
                        FileSize BIGINT,
                        DocumentTypeName VARCHAR(255),
                        DocumentTypeCategory VARCHAR(100),
                        Status INT,
                        ProcessingStatus VARCHAR(50),
                        Summary TEXT,
                        UploadedAt TIMESTAMP,
                        ProcessedAt TIMESTAMP,
                        ProcessingStartedAt TIMESTAMP,
                        ProcessingCompletedAt TIMESTAMP
                    )
                    AS $$
                    BEGIN
                        RETURN QUERY
                        SELECT
                            d.id,
                            d.filename,
                            d.fileextension,
                            d.storagepath,
                            d.filesize,
                            d.documenttypename,
                            d.documenttypecategory,
                            d.status,
                            d.processingstatus,
                            d.summary,
                            d.uploadedat,
                            d.processedat,
                            d.processingstartedat,
                            d.processingcompletedat
                        FROM dps_dbo.documents d
                        WHERE d.isdeleted = 0
                            AND d.uploadedat >= (NOW() AT TIME ZONE 'UTC') - (p_Days || ' days')::INTERVAL
                            AND (p_Status IS NULL OR d.status = p_Status)
                            AND (p_DocumentTypeName IS NULL OR d.documenttypename = p_DocumentTypeName)
                        ORDER BY d.uploadedat DESC;
                    END;
                    $$ LANGUAGE plpgsql;
                ");
                logger.LogInformation("Created stored function: sp_GetRecentDocuments");
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
    /// Builds a PostgreSQL connection string from AWS Secrets Manager
    /// </summary>
    private static async Task<string> BuildConnectionStringFromSecretsManager()
    {
        var secretsService = new SecretsManagerService();

        // Get all connection info from secret with description starting with "Password for RDS MSSQL used for MAM319."
        var secretJson = await secretsService.GetSecretByDescriptionPrefixAsync("Password for RDS MSSQL used for MAM319.");
        var username = secretsService.GetFieldFromSecret(secretJson, "username");
        var password = secretsService.GetFieldFromSecret(secretJson, "password");
        var host = secretsService.GetFieldFromSecret(secretJson, "host");
        var port = secretsService.GetFieldFromSecret(secretJson, "port");
        var dbname = secretsService.GetFieldFromSecret(secretJson, "dbname");

        // Build PostgreSQL connection string
        var connectionString = $"Host={host};Port={port};Database={dbname};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";

        return connectionString;
    }
}