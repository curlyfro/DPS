using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentProcessor.Infrastructure.BackgroundTasks
{
    /// <summary>
    /// Background service that processes items from the AI processing queue (database-backed)
    /// </summary>
    public class AIQueueProcessingService : BackgroundService
    {
        private readonly IAIProcessingQueue _processingQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AIQueueProcessingService> _logger;
        private readonly int _pollingIntervalSeconds;

        public AIQueueProcessingService(
            IAIProcessingQueue processingQueue,
            IServiceProvider serviceProvider,
            ILogger<AIQueueProcessingService> logger)
        {
            _processingQueue = processingQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _pollingIntervalSeconds = 5; // Poll every 5 seconds
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AI Queue Processing Service is starting.");

            // Wait a moment for all services to initialize
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            _logger.LogInformation("AI Queue Processing Service initialized and ready to process.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Checking for items in queue...");
                    
                    // Try to dequeue an item from the database queue
                    var queueItem = await _processingQueue.DequeueNextAsync();
                    
                    if (queueItem != null)
                    {
                        _logger.LogInformation($"Dequeued document {queueItem.DocumentId} from queue {queueItem.QueueId}");
                        
                        // Process the document
                        await ProcessDocumentAsync(queueItem, stoppingToken);
                    }
                    else
                    {
                        _logger.LogDebug("No items found in queue, waiting...");
                        // No items in queue, wait before checking again
                        await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in AI Queue Processing Service");
                    
                    // Wait a bit before retrying to avoid tight error loops
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("AI Queue Processing Service is stopping.");
        }

        private async Task ProcessDocumentAsync(ProcessingQueueItem queueItem, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            
            try
            {
                _logger.LogInformation($"Starting processing for document {queueItem.DocumentId}");
                
                // Get the document processing service dynamically
                // This avoids circular dependency between Infrastructure and Application layers
                var processingServiceType = Type.GetType("DocumentProcessor.Application.Services.IDocumentProcessingService, DocumentProcessor.Application");
                if (processingServiceType == null)
                {
                    _logger.LogError("Could not load IDocumentProcessingService type");
                    return;
                }
                
                var processingService = scope.ServiceProvider.GetService(processingServiceType);
                if (processingService == null)
                {
                    _logger.LogError("IDocumentProcessingService not found in service provider");
                    return;
                }
                
                // Process the document using reflection
                var processMethod = processingServiceType.GetMethod("ProcessDocumentAsync");
                if (processMethod == null)
                {
                    _logger.LogError("ProcessDocumentAsync method not found");
                    return;
                }
                
                var resultTask = processMethod.Invoke(processingService, new object[] { queueItem.DocumentId, null }) as Task;
                if (resultTask == null)
                {
                    _logger.LogError("Failed to invoke ProcessDocumentAsync");
                    return;
                }
                
                await resultTask;
                
                // Get the result using reflection
                var resultProperty = resultTask.GetType().GetProperty("Result");
                dynamic result = resultProperty?.GetValue(resultTask);
                
                if (result == null)
                {
                    _logger.LogError("Failed to get processing result");
                    return;
                }
                
                bool success = (bool)result.Success;
                
                if (success)
                {
                    _logger.LogInformation($"Successfully processed document {queueItem.DocumentId}");
                    
                    // Update queue status to completed
                    if (_processingQueue is AI.DatabaseProcessingQueue dbQueue)
                    {
                        var resultData = new
                        {
                            Classification = result.Classification,
                            Extraction = result.Extraction,
                            Summary = result.Summary,
                            Intent = result.Intent
                        };
                        var resultJson = JsonSerializer.Serialize(resultData);
                        await dbQueue.CompleteProcessingAsync(queueItem.QueueId, resultJson);
                    }
                }
                else
                {
                    string errorMessage = (string)(result.ErrorMessage ?? "Processing failed");
                    _logger.LogError("Failed to process document {DocumentId}: {ErrorMessage}", 
                        queueItem.DocumentId, errorMessage);
                    
                    // Update queue status to failed
                    if (_processingQueue is AI.DatabaseProcessingQueue dbQueue)
                    {
                        await dbQueue.FailProcessingAsync(queueItem.QueueId, 
                            errorMessage, 
                            null);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing document {queueItem.DocumentId}");
                
                // Update queue status to failed or retry
                if (_processingQueue is AI.DatabaseProcessingQueue dbQueue)
                {
                    await dbQueue.FailProcessingAsync(queueItem.QueueId,
                        ex.Message,
                        ex.ToString());
                }
                
                // Don't re-throw the exception to prevent the service from crashing
                // The item has been marked as failed and can be retried later
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("AI Queue Processing Service is stopping.");
            await base.StopAsync(cancellationToken);
        }
    }
}