using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.Json;

namespace DocumentProcessor.Application.Services
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly IAIProcessorFactory _aiProcessorFactory;
        private readonly IAIProcessingQueue _processingQueue;
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentSourceProvider _documentSource;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DocumentProcessingService> _logger;

        public DocumentProcessingService(
            IAIProcessorFactory aiProcessorFactory,
            IAIProcessingQueue processingQueue,
            IDocumentRepository documentRepository,
            IDocumentSourceProvider documentSource,
            IUnitOfWork unitOfWork,
            ILogger<DocumentProcessingService> logger)
        {
            _aiProcessorFactory = aiProcessorFactory;
            _processingQueue = processingQueue;
            _documentRepository = documentRepository;
            _documentSource = documentSource;
            _unitOfWork = unitOfWork;
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

                // Get document content - create separate streams for each AI operation
                // to avoid "Cannot access a closed file" errors
                using var classificationStream = await _documentSource.GetDocumentStreamAsync(document.StoragePath);
                using var extractionStream = await _documentSource.GetDocumentStreamAsync(document.StoragePath);
                using var summaryStream = await _documentSource.GetDocumentStreamAsync(document.StoragePath);
                using var intentStream = await _documentSource.GetDocumentStreamAsync(document.StoragePath);

                // Process with AI - each operation gets its own stream
                var classificationTask = processor.ClassifyDocumentAsync(document, classificationStream);
                var extractionTask = processor.ExtractDataAsync(document, extractionStream);
                var summaryTask = processor.GenerateSummaryAsync(document, summaryStream);
                var intentTask = processor.DetectIntentAsync(document, intentStream);

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
                
                // Store the summary directly without prepending classification
                if (result.Summary != null && !string.IsNullOrEmpty(result.Summary.Summary))
                {
                    document.Summary = result.Summary.Summary;
                }

                // Store extracted text from extraction results
                if (result.Extraction != null && result.Extraction.Entities != null && result.Extraction.Entities.Any())
                {
                    // Combine all extracted entities into a text representation
                    var extractedTexts = result.Extraction.Entities
                        .Select(e => $"{e.Type}: {e.Value}")
                        .ToList();
                    document.ExtractedText = string.Join("; ", extractedTexts);
                }

                // Handle document type and classification
                if (result.Classification != null && !string.IsNullOrEmpty(result.Classification.PrimaryCategory))
                {
                    // Look up or create DocumentType based on classification
                    var documentType = await _unitOfWork.DocumentTypes.GetByNameAsync(result.Classification.PrimaryCategory);
                    if (documentType == null)
                    {
                        documentType = new DocumentType
                        {
                            Id = Guid.NewGuid(),
                            Name = result.Classification.PrimaryCategory,
                            Description = $"Auto-generated type for {result.Classification.PrimaryCategory}",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.DocumentTypes.AddAsync(documentType);
                    }
                    document.DocumentTypeId = documentType.Id;

                    // Create classification record
                    var classification = new Classification
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        DocumentTypeId = documentType.Id,
                        ConfidenceScore = result.Classification.CategoryConfidences?.FirstOrDefault().Value ?? 0.95,
                        ClassifiedAt = DateTime.UtcNow,
                        Method = ClassificationMethod.AI,
                        AIModelUsed = processor.ModelId,
                        AIResponse = JsonSerializer.Serialize(result.Classification),
                        ExtractedIntents = result.Intent != null ? JsonSerializer.Serialize(new[] { result.Intent.PrimaryIntent }) : null,
                        ExtractedEntities = result.Extraction?.Entities != null ? JsonSerializer.Serialize(result.Extraction.Entities) : null,
                        IsManuallyVerified = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Classifications.AddAsync(classification);

                    // If we still don't have extracted text, add classification info
                    if (string.IsNullOrEmpty(document.ExtractedText))
                    {
                        document.ExtractedText = $"Classification: {result.Classification.PrimaryCategory}";
                        if (result.Classification.Tags != null && result.Classification.Tags.Any())
                        {
                            document.ExtractedText += $"; Tags: {string.Join(", ", result.Classification.Tags)}";
                        }
                    }
                }

                // Create or update document metadata
                var existingMetadata = await _unitOfWork.DocumentMetadata.GetByDocumentIdAsync(document.Id);
                if (existingMetadata == null)
                {
                    var metadata = new DocumentMetadata
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        Title = document.FileName,
                        Author = "System", // Default author
                        CreationDate = document.UploadedAt,
                        ModificationDate = DateTime.UtcNow,
                        Language = result.Summary?.Language ?? "en",
                        PageCount = 1, // Default page count
                        WordCount = document.ExtractedText?.Split(' ').Length ?? 0,
                        Keywords = result.Classification?.Tags != null ? string.Join(", ", result.Classification.Tags) : "",
                        Subject = result.Classification?.PrimaryCategory ?? "",
                        Tags = new Dictionary<string, string>(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // Add extracted entities as tags
                    if (result.Extraction?.Entities != null)
                    {
                        foreach (var entity in result.Extraction.Entities.Take(10)) // Limit to 10 entities
                        {
                            metadata.Tags[entity.Type] = entity.Value;
                        }
                    }

                    await _unitOfWork.DocumentMetadata.AddAsync(metadata);
                }
                else
                {
                    // Update existing metadata
                    existingMetadata.ModificationDate = DateTime.UtcNow;
                    existingMetadata.Language = result.Summary?.Language ?? existingMetadata.Language;
                    existingMetadata.WordCount = document.ExtractedText?.Split(' ').Length ?? existingMetadata.WordCount;
                    existingMetadata.Keywords = result.Classification?.Tags != null ? string.Join(", ", result.Classification.Tags) : existingMetadata.Keywords;
                    existingMetadata.Subject = result.Classification?.PrimaryCategory ?? existingMetadata.Subject;
                    existingMetadata.UpdatedAt = DateTime.UtcNow;

                    // Update tags with extracted entities
                    if (result.Extraction?.Entities != null)
                    {
                        foreach (var entity in result.Extraction.Entities.Take(10))
                        {
                            existingMetadata.Tags[entity.Type] = entity.Value;
                        }
                    }

                    await _unitOfWork.DocumentMetadata.UpdateAsync(existingMetadata);
                }

                await _documentRepository.UpdateAsync(document);
                
                // Save all changes through unit of work
                await _unitOfWork.SaveChangesAsync();

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