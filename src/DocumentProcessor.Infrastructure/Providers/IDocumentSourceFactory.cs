using DocumentProcessor.Core.Interfaces;

namespace DocumentProcessor.Infrastructure.Providers;

/// <summary>
/// Interface for document source factory
/// </summary>
public interface IDocumentSourceFactory
{
    /// <summary>
    /// Creates the default document source provider
    /// </summary>
    IDocumentSourceProvider CreateProvider();

    /// <summary>
    /// Creates a specific document source provider by name
    /// </summary>
    IDocumentSourceProvider CreateProvider(string providerName);

    /// <summary>
    /// Gets all available provider names
    /// </summary>
    IEnumerable<string> GetAvailableProviders();
}