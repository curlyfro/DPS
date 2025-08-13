using System;
using System.Collections.Generic;
using System.Linq;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Providers
{
    /// <summary>
    /// Factory for creating document source providers based on configuration
    /// Implements strategy pattern for provider selection
    /// </summary>
    public class DocumentSourceFactory : IDocumentSourceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentSourceFactory> _logger;
        private readonly Dictionary<string, Type> _providerTypes;

        public DocumentSourceFactory(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<DocumentSourceFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;

            // Register available provider types
            _providerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["LocalFileSystem"] = typeof(LocalFileSystemProvider),
                //["MockS3"] = typeof(MockS3Provider),
                //["S3"] = typeof(MockS3Provider), // Using MockS3Provider until real S3 is implemented
                ["FileShare"] = typeof(FileShareProvider),
                ["Network"] = typeof(FileShareProvider) // Alias for FileShare
            };
        }

        /// <summary>
        /// Creates a document source provider based on the configured default provider
        /// </summary>
        public IDocumentSourceProvider CreateProvider()
        {
            var defaultProviderName = _configuration["DocumentSource:DefaultProvider"] ?? "LocalFileSystem";
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

                _logger.LogInformation("Creating document source provider: {ProviderName}", providerName);

                // Use dependency injection to create the provider with proper dependencies
                var provider = (IDocumentSourceProvider)_serviceProvider.GetRequiredService(providerType);
                
                _logger.LogInformation("Successfully created {ProviderName} provider", provider.ProviderName);
                return provider;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document source provider: {ProviderName}", providerName);
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

        /// <summary>
        /// Checks if a provider is available
        /// </summary>
        public bool IsProviderAvailable(string providerName)
        {
            return !string.IsNullOrWhiteSpace(providerName) && 
                   _providerTypes.ContainsKey(providerName);
        }

        /// <summary>
        /// Creates a provider based on document source type
        /// Useful for automatic provider selection based on path patterns
        /// </summary>
        public IDocumentSourceProvider CreateProviderForSource(string sourcePath)
        {
            try
            {
                // Determine provider based on path pattern
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    return CreateProvider(); // Use default
                }

                // Check for S3 URL pattern
                if (sourcePath.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ||
                    sourcePath.StartsWith("https://s3", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Detected S3 path pattern, using S3 provider");
                    return CreateProvider("S3");
                }

                // Check for UNC path pattern (network share)
                if (sourcePath.StartsWith("\\\\") || 
                    sourcePath.StartsWith("//") ||
                    sourcePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Detected network path pattern, using FileShare provider");
                    return CreateProvider("FileShare");
                }

                // Check for absolute local path
                if (Path.IsPathRooted(sourcePath))
                {
                    _logger.LogInformation("Detected local path pattern, using LocalFileSystem provider");
                    return CreateProvider("LocalFileSystem");
                }

                // Default to configured provider
                _logger.LogInformation("Using default provider for path: {Path}", sourcePath);
                return CreateProvider();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining provider for source: {SourcePath}", sourcePath);
                throw;
            }
        }

        /// <summary>
        /// Registers a custom provider type
        /// Useful for extending with additional providers at runtime
        /// </summary>
        public void RegisterProvider(string name, Type providerType)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Provider name cannot be null or empty.", nameof(name));
            }

            if (providerType == null)
            {
                throw new ArgumentNullException(nameof(providerType));
            }

            if (!typeof(IDocumentSourceProvider).IsAssignableFrom(providerType))
            {
                throw new ArgumentException(
                    $"Provider type must implement {nameof(IDocumentSourceProvider)}.", 
                    nameof(providerType));
            }

            _providerTypes[name] = providerType;
            _logger.LogInformation("Registered custom provider: {Name} -> {Type}", name, providerType.Name);
        }
    }

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

        /// <summary>
        /// Checks if a provider is available
        /// </summary>
        bool IsProviderAvailable(string providerName);

        /// <summary>
        /// Creates a provider based on document source type
        /// </summary>
        IDocumentSourceProvider CreateProviderForSource(string sourcePath);

        /// <summary>
        /// Registers a custom provider type
        /// </summary>
        void RegisterProvider(string name, Type providerType);
    }

    /// <summary>
    /// Multi-provider wrapper that can route operations to different providers
    /// Useful for scenarios where documents might come from multiple sources
    /// </summary>
    public class MultiSourceProvider : IDocumentSourceProvider
    {
        private readonly IDocumentSourceFactory _factory;
        private readonly ILogger<MultiSourceProvider> _logger;
        private readonly Dictionary<string, IDocumentSourceProvider> _providerCache;

        public string ProviderName => "MultiSource";

        public MultiSourceProvider(
            IDocumentSourceFactory factory,
            ILogger<MultiSourceProvider> logger)
        {
            _factory = factory;
            _logger = logger;
            _providerCache = new Dictionary<string, IDocumentSourceProvider>();
        }

        private IDocumentSourceProvider GetProviderForPath(string path)
        {
            // Extract provider hint from path if present (e.g., "s3:path" or "local:path")
            var colonIndex = path.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 10) // Reasonable limit for provider prefix
            {
                var providerHint = path.Substring(0, colonIndex);
                if (_factory.IsProviderAvailable(providerHint))
                {
                    if (!_providerCache.TryGetValue(providerHint, out var cachedProvider))
                    {
                        cachedProvider = _factory.CreateProvider(providerHint);
                        _providerCache[providerHint] = cachedProvider;
                    }
                    return cachedProvider;
                }
            }

            // Use factory to determine provider based on path pattern
            return _factory.CreateProviderForSource(path);
        }

        private string NormalizePath(string path)
        {
            // Remove provider prefix if present
            var colonIndex = path.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 10)
            {
                var providerHint = path.Substring(0, colonIndex);
                if (_factory.IsProviderAvailable(providerHint))
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
            _logger.LogDebug("Routing GetDocumentStreamAsync to {Provider} for path: {Path}", 
                provider.ProviderName, normalizedPath);
            return await provider.GetDocumentStreamAsync(normalizedPath);
        }

        public async Task<byte[]> GetDocumentBytesAsync(string path)
        {
            var provider = GetProviderForPath(path);
            var normalizedPath = NormalizePath(path);
            _logger.LogDebug("Routing GetDocumentBytesAsync to {Provider} for path: {Path}", 
                provider.ProviderName, normalizedPath);
            return await provider.GetDocumentBytesAsync(normalizedPath);
        }

        public async Task<string> SaveDocumentAsync(Stream documentStream, string fileName)
        {
            var provider = _factory.CreateProvider(); // Use default for saves
            _logger.LogDebug("Routing SaveDocumentAsync to {Provider} for file: {FileName}", 
                provider.ProviderName, fileName);
            return await provider.SaveDocumentAsync(documentStream, fileName);
        }

        public async Task<string> SaveDocumentAsync(byte[] documentBytes, string fileName)
        {
            var provider = _factory.CreateProvider(); // Use default for saves
            _logger.LogDebug("Routing SaveDocumentAsync to {Provider} for file: {FileName}", 
                provider.ProviderName, fileName);
            return await provider.SaveDocumentAsync(documentBytes, fileName);
        }

        public async Task<bool> DeleteDocumentAsync(string path)
        {
            var provider = GetProviderForPath(path);
            var normalizedPath = NormalizePath(path);
            _logger.LogDebug("Routing DeleteDocumentAsync to {Provider} for path: {Path}", 
                provider.ProviderName, normalizedPath);
            return await provider.DeleteDocumentAsync(normalizedPath);
        }

        public async Task<bool> DocumentExistsAsync(string path)
        {
            var provider = GetProviderForPath(path);
            var normalizedPath = NormalizePath(path);
            _logger.LogDebug("Routing DocumentExistsAsync to {Provider} for path: {Path}", 
                provider.ProviderName, normalizedPath);
            return await provider.DocumentExistsAsync(normalizedPath);
        }

        public async Task<DocumentInfo> GetDocumentInfoAsync(string path)
        {
            var provider = GetProviderForPath(path);
            var normalizedPath = NormalizePath(path);
            _logger.LogDebug("Routing GetDocumentInfoAsync to {Provider} for path: {Path}", 
                provider.ProviderName, normalizedPath);
            return await provider.GetDocumentInfoAsync(normalizedPath);
        }

        public async Task<IEnumerable<DocumentInfo>> ListDocumentsAsync(string path)
        {
            var provider = GetProviderForPath(path);
            var normalizedPath = NormalizePath(path);
            _logger.LogDebug("Routing ListDocumentsAsync to {Provider} for path: {Path}", 
                provider.ProviderName, normalizedPath);
            return await provider.ListDocumentsAsync(normalizedPath);
        }

        public async Task<string> GetDownloadUrlAsync(string path, TimeSpan expiration)
        {
            var provider = GetProviderForPath(path);
            var normalizedPath = NormalizePath(path);
            _logger.LogDebug("Routing GetDownloadUrlAsync to {Provider} for path: {Path}", 
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
                _logger.LogInformation("Performing cross-provider move from {Source} to {Dest}", 
                    sourceProvider.ProviderName, destProvider.ProviderName);
                
                var bytes = await sourceProvider.GetDocumentBytesAsync(NormalizePath(sourcePath));
                var fileName = Path.GetFileName(NormalizePath(destinationPath));
                await destProvider.SaveDocumentAsync(bytes, fileName);
                return await sourceProvider.DeleteDocumentAsync(NormalizePath(sourcePath));
            }

            // Same provider move
            var normalizedSource = NormalizePath(sourcePath);
            var normalizedDest = NormalizePath(destinationPath);
            _logger.LogDebug("Routing MoveDocumentAsync to {Provider}", sourceProvider.ProviderName);
            return await sourceProvider.MoveDocumentAsync(normalizedSource, normalizedDest);
        }

        public async Task<bool> CopyDocumentAsync(string sourcePath, string destinationPath)
        {
            var sourceProvider = GetProviderForPath(sourcePath);
            var destProvider = GetProviderForPath(destinationPath);

            if (sourceProvider.GetType() != destProvider.GetType())
            {
                // Cross-provider copy
                _logger.LogInformation("Performing cross-provider copy from {Source} to {Dest}", 
                    sourceProvider.ProviderName, destProvider.ProviderName);
                
                var bytes = await sourceProvider.GetDocumentBytesAsync(NormalizePath(sourcePath));
                var fileName = Path.GetFileName(NormalizePath(destinationPath));
                await destProvider.SaveDocumentAsync(bytes, fileName);
                return true;
            }

            // Same provider copy
            var normalizedSource = NormalizePath(sourcePath);
            var normalizedDest = NormalizePath(destinationPath);
            _logger.LogDebug("Routing CopyDocumentAsync to {Provider}", sourceProvider.ProviderName);
            return await sourceProvider.CopyDocumentAsync(normalizedSource, normalizedDest);
        }
    }
}