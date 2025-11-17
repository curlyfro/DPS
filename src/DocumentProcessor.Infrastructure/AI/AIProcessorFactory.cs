using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.Infrastructure.AI;

public class AIProcessorFactory : IAIProcessorFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIProcessorFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<AIProviderType, Func<IAIProcessor>> _processorFactories;

    public AIProcessorFactory(
        IConfiguration configuration,
        ILogger<AIProcessorFactory> logger,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
            
        _processorFactories = new Dictionary<AIProviderType, Func<IAIProcessor>>
        {
            [AIProviderType.AmazonBedrock] = CreateBedrockProcessor,
            // Future providers will be registered here
            // [AIProviderType.OpenAI] = () => new OpenAIProcessor(_configuration, _logger),
        };
    }

    public IAIProcessor CreateBedrockProcessor()
    {
        var bedrockOptions = new BedrockOptions();
        _configuration.GetSection("Bedrock").Bind(bedrockOptions);

        // Get or create DocumentContentExtractor
        var contentExtractorLogger = _serviceProvider.GetService<ILogger<DocumentContentExtractor>>();
        var contentExtractor = _serviceProvider.GetService<DocumentContentExtractor>();

        if (contentExtractor == null)
        {
            // Create DocumentContentExtractor if not registered
            contentExtractor = new DocumentContentExtractor(
                contentExtractorLogger ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentContentExtractor>());
        }

        return new BedrockAIProcessor(
            _loggerFactory.CreateLogger<BedrockAIProcessor>(),
            Options.Create(bedrockOptions),
            contentExtractor);
    }

    public IAIProcessor CreateProcessor(AIProviderType providerType)
    {
        if (!_processorFactories.TryGetValue(providerType, out var factory))
        {
            _logger.LogWarning("AI Provider {ProviderType} not available, falling back to Mock provider", providerType);
            return _processorFactories[AIProviderType.AmazonBedrock]();
        }

        try
        {
            var processor = factory();
            _logger.LogInformation("Created AI processor: {ProviderName} ({ModelId})", processor.ProviderName, processor.ModelId);
            return processor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create AI processor for {ProviderType}, falling back to AmazonBedrock", providerType);
            return _processorFactories[AIProviderType.AmazonBedrock]();
        }
    }

    public IAIProcessor GetDefaultProcessor()
    {
        var defaultProvider = _configuration.GetValue<string>("AIProcessing:DefaultProvider");
            
        if (Enum.TryParse<AIProviderType>(defaultProvider, out var providerType))
        {
            return CreateProcessor(providerType);
        }

        _logger.LogInformation("No default provider configured, using AmazonBedrock provider");
        return CreateProcessor(AIProviderType.AmazonBedrock);
    }
}