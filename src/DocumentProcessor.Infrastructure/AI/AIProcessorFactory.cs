using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.Infrastructure.AI
{
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
                [AIProviderType.AmazonBedrock] = () => CreateBedrockProcessor(),
                // Future providers will be registered here
                // [AIProviderType.OpenAI] = () => new OpenAIProcessor(_configuration, _logger),
            };
        }

        private IAIProcessor CreateBedrockProcessor()
        {
            var bedrockOptions = new BedrockOptions();
            _configuration.GetSection("Bedrock").Bind(bedrockOptions);
            
            return new BedrockAIProcessor(
                _loggerFactory.CreateLogger<BedrockAIProcessor>(),
                Options.Create(bedrockOptions),
                _serviceProvider);
        }

        public IAIProcessor CreateProcessor(AIProviderType providerType)
        {
            if (!_processorFactories.ContainsKey(providerType))
            {
                _logger.LogWarning($"AI Provider {providerType} not available, falling back to Mock provider");
                return _processorFactories[AIProviderType.AmazonBedrock]();
            }

            try
            {
                var processor = _processorFactories[providerType]();
                _logger.LogInformation($"Created AI processor: {processor.ProviderName} ({processor.ModelId})");
                return processor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create AI processor for {providerType}, falling back to AmazonBedrock");
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

        public IEnumerable<AIProviderType> GetAvailableProviders()
        {
            return _processorFactories.Keys;
        }
    }
}