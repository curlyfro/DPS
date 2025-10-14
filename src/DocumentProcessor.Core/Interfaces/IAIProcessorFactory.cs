namespace DocumentProcessor.Core.Interfaces
{
    public interface IAIProcessorFactory
    {
        IAIProcessor CreateProcessor(AIProviderType providerType);
        IAIProcessor GetDefaultProcessor();
        IEnumerable<AIProviderType> GetAvailableProviders();
    }

    public enum AIProviderType
    {
        AmazonBedrock,
        OpenAI,
        AzureOpenAI,
        GoogleVertexAI
    }
}