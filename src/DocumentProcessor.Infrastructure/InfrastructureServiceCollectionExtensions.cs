using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.AI;
using DocumentProcessor.Infrastructure.BackgroundTasks;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Infrastructure.Providers;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure
{
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add Entity Framework
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // Register repositories
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IDocumentTypeRepository, DocumentTypeRepository>();
            services.AddScoped<IClassificationRepository, ClassificationRepository>();
            services.AddScoped<IProcessingQueueRepository, ProcessingQueueRepository>();
            services.AddScoped<IDocumentMetadataRepository, DocumentMetadataRepository>();

            // Register document source providers
            services.AddSingleton<IDocumentSourceFactory, DocumentSourceFactory>();
            services.AddScoped<IDocumentSourceProvider>(provider =>
            {
                var factory = provider.GetRequiredService<IDocumentSourceFactory>();
                var sourceType = configuration.GetValue<string>("DocumentStorage:Provider") ?? "LocalFileSystem";
                return factory.CreateProvider(sourceType);
            });

            // Register AI processing services
            services.AddSingleton<IAIProcessorFactory, AIProcessorFactory>();
            services.AddSingleton<IAIProcessingQueue, InMemoryProcessingQueue>();
            
            // Register Bedrock configuration
            var bedrockSection = configuration.GetSection("Bedrock");
            services.Configure<BedrockOptions>(options =>
            {
                bedrockSection.Bind(options);
            });
            services.AddSingleton<MockAIProcessor>();
            
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
            services.AddHostedService<DocumentProcessingHostedService>(provider =>
                new DocumentProcessingHostedService(
                    provider.GetRequiredService<IBackgroundTaskQueue>(),
                    provider.GetRequiredService<ILogger<DocumentProcessingHostedService>>(),
                    maxConcurrency));
            
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
    }
}