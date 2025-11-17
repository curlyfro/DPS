using DocumentProcessor.Core.Interfaces;

namespace DocumentProcessor.Application.Services;

public interface IDocumentProcessingService
{
    Task<Guid> QueueDocumentForProcessingAsync(Guid documentId);
    Task<DocumentProcessingResult> ProcessDocumentAsync(Guid documentId, AIProviderType? providerType = null);
}