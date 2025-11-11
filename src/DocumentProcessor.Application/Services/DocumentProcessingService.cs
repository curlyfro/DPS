using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json;

namespace DocumentProcessor.Application.Services;

public class DocumentProcessingService(
    IAIProcessorFactory aiProcessorFactory,
    IDocumentSourceProvider documentSource,
    IServiceScopeFactory serviceScopeFactory,
    IBackgroundTaskQueue taskQueue,
    ILogger<DocumentProcessingService> logger)
    : IDocumentProcessingService
{
    public async Task<Guid> QueueDocumentForProcessingAsync(Guid documentId)
    {
        // Use a separate service scope to avoid tracking conflicts
        using var scope = serviceScopeFactory.CreateScope();
        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var document = await documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            throw new ArgumentException($"Document with ID {documentId} not found");
        }

        // Update document status and processing fields
        document.Status = DocumentStatus.Queued;
        document.ProcessingStatus = "Queued";
        document.ProcessingRetryCount = 0;
        document.UpdatedAt = DateTime.UtcNow;
        await documentRepository.UpdateAsync(document);

        // Queue to the background task queue so workers can pick it up
        var taskId = $"doc-{documentId}";
        await taskQueue.QueueBackgroundWorkItemAsync(
            async (cancellationToken) =>
            {
                await ProcessDocumentInternalAsync(documentId, cancellationToken);
            },
            taskId,
            priority: 0);

        logger.LogInformation("Document {DocumentId} queued for processing with task ID {TaskId}", documentId, taskId);
        return documentId;
    }

    private async ValueTask ProcessDocumentInternalAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting background processing for document {DocumentId}", documentId);

            // Process the document
            var result = await ProcessDocumentAsync(documentId);

            if (result.Success)
            {
                logger.LogInformation("Successfully processed document {DocumentId}", documentId);
            }
            else
            {
                logger.LogError("Failed to process document {DocumentId}: {Error}", documentId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document {DocumentId} in background", documentId);
            throw;
        }
    }

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(Guid documentId, AIProviderType? providerType = null)
    {
        // Use a separate service scope to avoid tracking conflicts
        using var scope = serviceScopeFactory.CreateScope();
        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var document = await documentRepository.GetByIdAsync(documentId);
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
            // Update document status and processing fields
            document.Status = DocumentStatus.Processing;
            document.ProcessingStatus = "Processing";
            document.ProcessingStartedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            await documentRepository.UpdateAsync(document);

            // Get AI processor
            var processor = providerType.HasValue
                ? aiProcessorFactory.CreateProcessor(providerType.Value)
                : aiProcessorFactory.GetDefaultProcessor();

            logger.LogInformation("Processing document {DocumentId} with {ProviderName}", documentId, processor.ProviderName);

            // Get document content - create separate streams for each AI operation
            await using var classificationStream = await documentSource.GetDocumentStreamAsync(document.StoragePath);
            await using var summaryStream = await documentSource.GetDocumentStreamAsync(document.StoragePath);

            // Process with AI - each operation gets its own stream
            var classificationTask = processor.ClassifyDocumentAsync(document, classificationStream);
            var summaryTask = processor.GenerateSummaryAsync(document, summaryStream);

            // Wait for all tasks
            await Task.WhenAll(classificationTask, summaryTask);

            // Get results
            result.Classification = await classificationTask;
            result.Summary = await summaryTask;

            // Update document with processing results
            document.Status = DocumentStatus.Processed;
            document.ProcessedAt = DateTime.UtcNow;
            document.ProcessingStatus = "Completed";
            document.ProcessingCompletedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;

            // Store the summary
            if (result.Summary != null && !string.IsNullOrEmpty(result.Summary.Summary))
            {
                document.Summary = result.Summary.Summary;
            }

            // Handle document type and classification - store directly in document
            if (result.Classification != null && !string.IsNullOrEmpty(result.Classification.PrimaryCategory))
            {
                document.DocumentTypeName = result.Classification.PrimaryCategory;
                document.DocumentTypeCategory = result.Classification.PrimaryCategory;

                // If we still don't have extracted text, add classification info
                if (string.IsNullOrEmpty(document.ExtractedText))
                {
                    document.ExtractedText = $"Classification: {result.Classification.PrimaryCategory}";
                    if (result.Classification.Tags.Any())
                    {
                        document.ExtractedText += $"; Tags: {string.Join(", ", result.Classification.Tags)}";
                    }
                }
            }

            await documentRepository.UpdateAsync(document);

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;

            logger.LogInformation("Successfully processed document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document {DocumentId}", documentId);

            document.Status = DocumentStatus.Failed;
            document.ProcessingStatus = "Failed";
            document.ProcessingErrorMessage = ex.Message;
            document.ProcessingCompletedAt = DateTime.UtcNow;
            document.ProcessingRetryCount++;
            document.UpdatedAt = DateTime.UtcNow;
            await documentRepository.UpdateAsync(document);

            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

}