namespace DocumentProcessor.Core.Interfaces
{
    public interface IAIProcessorFactory
    {
        IAIProcessor CreateProcessor(AIProviderType providerType);
        IAIProcessor GetDefaultProcessor();
    }

    public enum AIProviderType
    {
        AmazonBedrock
    }
}