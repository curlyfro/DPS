using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Providers;

/// <summary>
/// Multi-provider wrapper that can route operations to different providers
/// Useful for scenarios where documents might come from multiple sources
/// </summary>
public class MultiSourceProvider(
    IDocumentSourceFactory factory,
    ILogger<MultiSourceProvider> logger)
    : IDocumentSourceProvider
{
    private readonly Dictionary<string, IDocumentSourceProvider> _providerCache = new();

    public string ProviderName => "MultiSource";

    private IDocumentSourceProvider GetProviderForPath(string path)
    {
        // Extract provider hint from path if present (e.g., "s3:path" or "local:path")
        var colonIndex = path.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 10) // Reasonable limit for provider prefix
        {
            var providerHint = path.Substring(0, colonIndex);
            if (factory.IsProviderAvailable(providerHint))
            {
                if (!_providerCache.TryGetValue(providerHint, out var cachedProvider))
                {
                    cachedProvider = factory.CreateProvider(providerHint);
                    _providerCache[providerHint] = cachedProvider;
                }
                return cachedProvider;
            }
        }

        // Use factory to determine provider based on path pattern
        return factory.CreateProviderForSource(path);
    }

    private string NormalizePath(string path)
    {
        // Remove provider prefix if present
        var colonIndex = path.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 10)
        {
            var providerHint = path.Substring(0, colonIndex);
            if (factory.IsProviderAvailable(providerHint))
            {
                return path.Substring(colonIndex + 1);
            }
        }
        return path;
    }

    public async Task<Stream> GetDocumentStreamAsync(string path)
    {
        var provider = GetProviderForPath(path);
        var normalizedPath = NormalizePath(path);
        logger.LogDebug("Routing GetDocumentStreamAsync to {Provider} for path: {Path}", 
            provider.ProviderName, normalizedPath);
        return await provider.GetDocumentStreamAsync(normalizedPath);
    }

    public async Task<byte[]> GetDocumentBytesAsync(string path)
    {
        var provider = GetProviderForPath(path);
        var normalizedPath = NormalizePath(path);
        logger.LogDebug("Routing GetDocumentBytesAsync to {Provider} for path: {Path}", 
            provider.ProviderName, normalizedPath);
        return await provider.GetDocumentBytesAsync(normalizedPath);
    }

    public async Task<string> SaveDocumentAsync(Stream documentStream, string fileName)
    {
        var provider = factory.CreateProvider(); // Use default for saves
        logger.LogDebug("Routing SaveDocumentAsync to {Provider} for file: {FileName}", 
            provider.ProviderName, fileName);
        return await provider.SaveDocumentAsync(documentStream, fileName);
    }

    public async Task<string> SaveDocumentAsync(byte[] documentBytes, string fileName)
    {
        var provider = factory.CreateProvider(); // Use default for saves
        logger.LogDebug("Routing SaveDocumentAsync to {Provider} for file: {FileName}", 
            provider.ProviderName, fileName);
        return await provider.SaveDocumentAsync(documentBytes, fileName);
    }

    public async Task<bool> DeleteDocumentAsync(string path)
    {
        var provider = GetProviderForPath(path);
        var normalizedPath = NormalizePath(path);
        logger.LogDebug("Routing DeleteDocumentAsync to {Provider} for path: {Path}", 
            provider.ProviderName, normalizedPath);
        return await provider.DeleteDocumentAsync(normalizedPath);
    }

    public async Task<bool> DocumentExistsAsync(string path)
    {
        var provider = GetProviderForPath(path);
        var normalizedPath = NormalizePath(path);
        logger.LogDebug("Routing DocumentExistsAsync to {Provider} for path: {Path}", 
            provider.ProviderName, normalizedPath);
        return await provider.DocumentExistsAsync(normalizedPath);
    }

    public async Task<DocumentInfo> GetDocumentInfoAsync(string path)
    {
        var provider = GetProviderForPath(path);
        var normalizedPath = NormalizePath(path);
        logger.LogDebug("Routing GetDocumentInfoAsync to {Provider} for path: {Path}", 
            provider.ProviderName, normalizedPath);
        return await provider.GetDocumentInfoAsync(normalizedPath);
    }

    public async Task<IEnumerable<DocumentInfo>> ListDocumentsAsync(string path)
    {
        var provider = GetProviderForPath(path);
        var normalizedPath = NormalizePath(path);
        logger.LogDebug("Routing ListDocumentsAsync to {Provider} for path: {Path}", 
            provider.ProviderName, normalizedPath);
        return await provider.ListDocumentsAsync(normalizedPath);
    }

    public async Task<string> GetDownloadUrlAsync(string path, TimeSpan expiration)
    {
        var provider = GetProviderForPath(path);
        var normalizedPath = NormalizePath(path);
        logger.LogDebug("Routing GetDownloadUrlAsync to {Provider} for path: {Path}", 
            provider.ProviderName, normalizedPath);
        return await provider.GetDownloadUrlAsync(normalizedPath, expiration);
    }

    public async Task<bool> MoveDocumentAsync(string sourcePath, string destinationPath)
    {
        var sourceProvider = GetProviderForPath(sourcePath);
        var destProvider = GetProviderForPath(destinationPath);

        if (sourceProvider.GetType() != destProvider.GetType())
        {
            // Cross-provider move: copy then delete
            logger.LogInformation("Performing cross-provider move from {Source} to {Dest}", 
                sourceProvider.ProviderName, destProvider.ProviderName);
                
            var bytes = await sourceProvider.GetDocumentBytesAsync(NormalizePath(sourcePath));
            var fileName = Path.GetFileName(NormalizePath(destinationPath));
            await destProvider.SaveDocumentAsync(bytes, fileName);
            return await sourceProvider.DeleteDocumentAsync(NormalizePath(sourcePath));
        }

        // Same provider move
        var normalizedSource = NormalizePath(sourcePath);
        var normalizedDest = NormalizePath(destinationPath);
        logger.LogDebug("Routing MoveDocumentAsync to {Provider}", sourceProvider.ProviderName);
        return await sourceProvider.MoveDocumentAsync(normalizedSource, normalizedDest);
    }

    public async Task<bool> CopyDocumentAsync(string sourcePath, string destinationPath)
    {
        var sourceProvider = GetProviderForPath(sourcePath);
        var destProvider = GetProviderForPath(destinationPath);

        if (sourceProvider.GetType() != destProvider.GetType())
        {
            // Cross-provider copy
            logger.LogInformation("Performing cross-provider copy from {Source} to {Dest}", 
                sourceProvider.ProviderName, destProvider.ProviderName);
                
            var bytes = await sourceProvider.GetDocumentBytesAsync(NormalizePath(sourcePath));
            var fileName = Path.GetFileName(NormalizePath(destinationPath));
            await destProvider.SaveDocumentAsync(bytes, fileName);
            return true;
        }

        // Same provider copy
        var normalizedSource = NormalizePath(sourcePath);
        var normalizedDest = NormalizePath(destinationPath);
        logger.LogDebug("Routing CopyDocumentAsync to {Provider}", sourceProvider.ProviderName);
        return await sourceProvider.CopyDocumentAsync(normalizedSource, normalizedDest);
    }
}