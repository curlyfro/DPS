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
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace DocumentProcessor.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Entity Framework
        // Build connection string from AWS Secrets Manager
        var connectionString = BuildConnectionStringFromSecretsManager().GetAwaiter().GetResult();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        // Register repositories
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentTypeRepository, DocumentTypeRepository>();
        services.AddScoped<IClassificationRepository, ClassificationRepository>();
        services.AddScoped<IProcessingQueueRepository, ProcessingQueueRepository>();
        services.AddScoped<IDocumentMetadataRepository, DocumentMetadataRepository>();
            
        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register document source providers
        services.AddSingleton<LocalFileSystemProvider>();
        services.AddSingleton<FileShareProvider>();
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
        
        // Register DocumentContentExtractor with service provider for transcription support
        services.AddScoped<DocumentContentExtractor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DocumentContentExtractor>>();
            return new DocumentContentExtractor(logger, provider);
        });

        // Register Bedrock configuration
        var bedrockSection = configuration.GetSection("Bedrock");
        services.Configure<BedrockOptions>(options =>
        {
            bedrockSection.Bind(options);
        });
            
        // Register background task services
        var usePriorityQueue = configuration.GetValue<bool>("BackgroundTasks:UsePriorityQueue", true);
        if (usePriorityQueue)
        {
            services.AddSingleton<IBackgroundTaskQueue, PriorityBackgroundTaskQueue>();
        }
        else
        {
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        }
            
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

        try
        {
            logger.LogInformation("Ensuring database exists and is up to date...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database is ready");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database");
            throw;
        }
    }

    /// <summary>
    /// Builds a PostgreSQL connection string from AWS Secrets Manager
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

        // Build PostgreSQL connection string
        var connectionString = $"Host={host};Port={port};Database=atx-aes-target;Username={username};Password={password}";

        return connectionString;
    }
}