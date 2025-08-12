using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.AI
{
    public class AIProcessorFactory : IAIProcessorFactory
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIProcessorFactory> _logger;
        private readonly Dictionary<AIProviderType, Func<IAIProcessor>> _processorFactories;

        public AIProcessorFactory(IConfiguration configuration, ILogger<AIProcessorFactory> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            _processorFactories = new Dictionary<AIProviderType, Func<IAIProcessor>>
            {
                [AIProviderType.Mock] = () => new MockAIProcessor(),
                // Future providers will be registered here
                // [AIProviderType.AmazonBedrock] = () => new BedrockAIProcessor(_configuration, _logger),
                // [AIProviderType.OpenAI] = () => new OpenAIProcessor(_configuration, _logger),
            };
        }

        public IAIProcessor CreateProcessor(AIProviderType providerType)
        {
            if (!_processorFactories.ContainsKey(providerType))
            {
                _logger.LogWarning($"AI Provider {providerType} not available, falling back to Mock provider");
                return _processorFactories[AIProviderType.Mock]();
            }

            try
            {
                var processor = _processorFactories[providerType]();
                _logger.LogInformation($"Created AI processor: {processor.ProviderName} ({processor.ModelId})");
                return processor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create AI processor for {providerType}, falling back to Mock");
                return _processorFactories[AIProviderType.Mock]();
            }
        }

        public IAIProcessor GetDefaultProcessor()
        {
            var defaultProvider = _configuration.GetValue<string>("AIProcessing:DefaultProvider");
            
            if (Enum.TryParse<AIProviderType>(defaultProvider, out var providerType))
            {
                return CreateProcessor(providerType);
            }

            _logger.LogInformation("No default provider configured, using Mock provider");
            return CreateProcessor(AIProviderType.Mock);
        }

        public IEnumerable<AIProviderType> GetAvailableProviders()
        {
            return _processorFactories.Keys;
        }
    }
}