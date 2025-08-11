using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Infrastructure.Providers;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure
{
    /// <summary>
    /// Extension methods for configuring infrastructure services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all infrastructure services to the service collection
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Add database context
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly("DocumentProcessor.Infrastructure");
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                    });
            });

            // Add repositories
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IDocumentTypeRepository, DocumentTypeRepository>();
            services.AddScoped<IClassificationRepository, ClassificationRepository>();
            services.AddScoped<IProcessingQueueRepository, ProcessingQueueRepository>();
            services.AddScoped<IDocumentMetadataRepository, DocumentMetadataRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Add document source providers
            services.AddScoped<LocalFileSystemProvider>();
            services.AddScoped<MockS3Provider>();
            services.AddScoped<FileShareProvider>();
            
            // Add document source factory
            services.AddScoped<IDocumentSourceFactory, DocumentSourceFactory>();
            services.AddScoped<MultiSourceProvider>();

            // Register default provider based on configuration
            services.AddScoped<IDocumentSourceProvider>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IDocumentSourceFactory>();
                var defaultProvider = configuration["DocumentSource:DefaultProvider"] ?? "LocalFileSystem";
                
                // Check if multi-source routing is enabled
                var useMultiSource = bool.TryParse(configuration["DocumentSource:UseMultiSource"], out var useMulti) && useMulti;
                if (useMultiSource)
                {
                    return serviceProvider.GetRequiredService<MultiSourceProvider>();
                }
                
                return factory.CreateProvider(defaultProvider);
            });

            return services;
        }

        /// <summary>
        /// Ensures the database is created and migrations are applied
        /// </summary>
        public static async Task EnsureDatabaseAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Apply any pending migrations
            await context.Database.MigrateAsync();
            
            // Seed initial data if needed
            await SeedDataAsync(context);
        }

        /// <summary>
        /// Seeds initial data if the database is empty
        /// </summary>
        private static async Task SeedDataAsync(ApplicationDbContext context)
        {
            // Check if data already exists
            if (await context.DocumentTypes.AnyAsync())
            {
                return; // Database already seeded
            }

            // Seed will be handled by the migration
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Adds health checks for infrastructure services
        /// </summary>
        public static IServiceCollection AddInfrastructureHealthChecks(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddHealthChecks()
                .AddDbContextCheck<ApplicationDbContext>("database")
                .AddCheck<DocumentSourceHealthCheck>("document-source");

            return services;
        }
    }

    /// <summary>
    /// Health check for document source providers
    /// </summary>
    public class DocumentSourceHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
    {
        private readonly IDocumentSourceProvider _provider;
        private readonly ILogger<DocumentSourceHealthCheck> _logger;

        public DocumentSourceHealthCheck(
            IDocumentSourceProvider provider,
            ILogger<DocumentSourceHealthCheck> logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to list documents in the root directory as a health check
                var documents = await _provider.ListDocumentsAsync("");
                
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    $"Document source provider '{_provider.ProviderName}' is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document source health check failed");
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    $"Document source provider '{_provider.ProviderName}' is unhealthy",
                    ex);
            }
        }
    }
}