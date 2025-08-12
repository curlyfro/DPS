using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Application.Services
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly IAIProcessorFactory _aiProcessorFactory;
        private readonly IAIProcessingQueue _processingQueue;
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentSourceProvider _documentSource;
        private readonly ILogger<DocumentProcessingService> _logger;

        public DocumentProcessingService(
            IAIProcessorFactory aiProcessorFactory,
            IAIProcessingQueue processingQueue,
            IDocumentRepository documentRepository,
            IDocumentSourceProvider documentSource,
            ILogger<DocumentProcessingService> logger)
        {
            _aiProcessorFactory = aiProcessorFactory;
            _processingQueue = processingQueue;
            _documentRepository = documentRepository;
            _documentSource = documentSource;
            _logger = logger;
        }

        public async Task<Guid> QueueDocumentForProcessingAsync(Guid documentId, ProcessingPriority priority = ProcessingPriority.Normal)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            // Update document status
            document.Status = DocumentStatus.Queued;
            await _documentRepository.UpdateAsync(document);

            // Add to processing queue
            var queueId = await _processingQueue.EnqueueDocumentAsync(documentId, priority);
            
            _logger.LogInformation($"Document {documentId} queued for processing with queue ID {queueId}");
            return queueId;
        }

        public async Task<DocumentProcessingResult> ProcessDocumentAsync(Guid documentId, AIProviderType? providerType = null)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            var result = new DocumentProcessingResult
            {
                DocumentId = documentId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // Update document status
                document.Status = DocumentStatus.Processing;
                await _documentRepository.UpdateAsync(document);

                // Get AI processor
                var processor = providerType.HasValue 
                    ? _aiProcessorFactory.CreateProcessor(providerType.Value)
                    : _aiProcessorFactory.GetDefaultProcessor();

                _logger.LogInformation($"Processing document {documentId} with {processor.ProviderName}");

                // Get document content
                using var contentStream = await _documentSource.GetDocumentStreamAsync(document.StoragePath);

                // Process with AI
                var classificationTask = processor.ClassifyDocumentAsync(document, contentStream);
                contentStream.Position = 0; // Reset stream position
                
                var extractionTask = processor.ExtractDataAsync(document, contentStream);
                contentStream.Position = 0;
                
                var summaryTask = processor.GenerateSummaryAsync(document, contentStream);
                contentStream.Position = 0;
                
                var intentTask = processor.DetectIntentAsync(document, contentStream);

                // Wait for all tasks
                await Task.WhenAll(classificationTask, extractionTask, summaryTask, intentTask);

                // Get results
                result.Classification = await classificationTask;
                result.Extraction = await extractionTask;
                result.Summary = await summaryTask;
                result.Intent = await intentTask;

                // Update document with processing results
                document.Status = DocumentStatus.Processed;
                document.ProcessedAt = DateTime.UtcNow;
                
                // Store primary classification
                if (result.Classification != null && !string.IsNullOrEmpty(result.Classification.PrimaryCategory))
                {
                    // You could store this in document metadata or a separate table
                    document.DocumentType = new DocumentType 
                    { 
                        Name = result.Classification.PrimaryCategory,
                        Description = $"Auto-classified as {result.Classification.PrimaryCategory}"
                    };
                }

                await _documentRepository.UpdateAsync(document);

                result.Success = true;
                result.CompletedAt = DateTime.UtcNow;
                
                _logger.LogInformation($"Successfully processed document {documentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing document {documentId}");
                
                document.Status = DocumentStatus.Failed;
                await _documentRepository.UpdateAsync(document);

                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }

            return result;
        }

        public async Task<ProcessingQueueStatus> GetProcessingStatusAsync(Guid queueId)
        {
            return await _processingQueue.GetQueueStatusAsync(queueId);
        }

        public async Task<IEnumerable<ProcessingQueueItem>> GetPendingDocumentsAsync()
        {
            return await _processingQueue.GetPendingItemsAsync();
        }

        public async Task CancelProcessingAsync(Guid queueId)
        {
            await _processingQueue.CancelProcessingAsync(queueId);
        }

        public async Task<ProcessingCost> EstimateProcessingCostAsync(Guid documentId, AIProviderType? providerType = null)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            var processor = providerType.HasValue 
                ? _aiProcessorFactory.CreateProcessor(providerType.Value)
                : _aiProcessorFactory.GetDefaultProcessor();

            return await processor.EstimateCostAsync(document.FileSize, document.ContentType);
        }
    }

    public interface IDocumentProcessingService
    {
        Task<Guid> QueueDocumentForProcessingAsync(Guid documentId, ProcessingPriority priority = ProcessingPriority.Normal);
        Task<DocumentProcessingResult> ProcessDocumentAsync(Guid documentId, AIProviderType? providerType = null);
        Task<ProcessingQueueStatus> GetProcessingStatusAsync(Guid queueId);
        Task<IEnumerable<ProcessingQueueItem>> GetPendingDocumentsAsync();
        Task CancelProcessingAsync(Guid queueId);
        Task<ProcessingCost> EstimateProcessingCostAsync(Guid documentId, AIProviderType? providerType = null);
    }

    public class DocumentProcessingResult
    {
        public Guid DocumentId { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public DocumentClassificationResult? Classification { get; set; }
        public DocumentExtractionResult? Extraction { get; set; }
        public DocumentSummaryResult? Summary { get; set; }
        public DocumentIntentResult? Intent { get; set; }
    }
}