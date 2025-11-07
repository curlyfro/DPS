using System;
using System.Collections.Generic;
using System.Linq;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Providers;

/// <summary>
/// Factory for creating document source providers based on configuration
/// Implements strategy pattern for provider selection
/// </summary>
public class DocumentSourceFactory(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DocumentSourceFactory> logger)
    : IDocumentSourceFactory
{
    private readonly Dictionary<string, Type> _providerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LocalFileSystem"] = typeof(LocalFileSystemProvider)
    };

    // Register available provider types
    //["MockS3"] = typeof(MockS3Provider),
    //["S3"] = typeof(MockS3Provider), // Using MockS3Provider until real S3 is implemented
    // Alias for FileShare

    /// <summary>
    /// Creates a document source provider based on the configured default provider
    /// </summary>
    public IDocumentSourceProvider CreateProvider()
    {
        var defaultProviderName = configuration["DocumentSource:DefaultProvider"] ?? "LocalFileSystem";
        return CreateProvider(defaultProviderName);
    }

    /// <summary>
    /// Creates a specific document source provider by name
    /// </summary>
    public IDocumentSourceProvider CreateProvider(string providerName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));
            }

            if (!_providerTypes.TryGetValue(providerName, out var providerType))
            {
                throw new NotSupportedException($"Document source provider '{providerName}' is not supported.");
            }

            logger.LogInformation("Creating document source provider: {ProviderName}", providerName);

            // Use dependency injection to create the provider with proper dependencies
            var provider = (IDocumentSourceProvider)serviceProvider.GetRequiredService(providerType);
                
            logger.LogInformation("Successfully created {ProviderName} provider", provider.ProviderName);
            return provider;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating document source provider: {ProviderName}", providerName);
            throw;
        }
    }

    /// <summary>
    /// Gets all available provider names
    /// </summary>
    public IEnumerable<string> GetAvailableProviders()
    {
        return _providerTypes.Keys;
    }
}