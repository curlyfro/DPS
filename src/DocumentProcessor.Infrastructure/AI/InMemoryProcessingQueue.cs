using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DocumentProcessor.Infrastructure.AI
{
    public class InMemoryProcessingQueue : IAIProcessingQueue
    {
        private readonly ConcurrentDictionary<Guid, ProcessingQueueStatus> _queueItems = new();
        private readonly ConcurrentQueue<ProcessingQueueItem> _processingQueue = new();
        private readonly ILogger<InMemoryProcessingQueue> _logger;
        private readonly SemaphoreSlim _queueSemaphore = new(1, 1);

        public InMemoryProcessingQueue(ILogger<InMemoryProcessingQueue> logger)
        {
            _logger = logger;
        }

        public async Task<Guid> EnqueueDocumentAsync(Guid documentId, ProcessingPriority priority = ProcessingPriority.Normal)
        {
            var queueId = Guid.NewGuid();
            var status = new ProcessingQueueStatus
            {
                QueueId = queueId,
                DocumentId = documentId,
                State = ProcessingState.Queued,
                Priority = priority,
                EnqueuedAt = DateTime.UtcNow,
                RetryCount = 0,
                Metadata = new Dictionary<string, object>()
            };

            var queueItem = new ProcessingQueueItem
            {
                QueueId = queueId,
                DocumentId = documentId,
                Priority = priority,
                EnqueuedAt = status.EnqueuedAt,
                RetryCount = 0
            };

            _queueItems[queueId] = status;
            
            await _queueSemaphore.WaitAsync();
            try
            {
                // Insert based on priority
                if (priority == ProcessingPriority.Critical || priority == ProcessingPriority.High)
                {
                    // For high priority, we need to reorder the queue
                    var tempItems = new List<ProcessingQueueItem>();
                    while (_processingQueue.TryDequeue(out var item))
                    {
                        tempItems.Add(item);
                    }

                    _processingQueue.Enqueue(queueItem);
                    
                    // Re-add items, placing high priority first
                    var sortedItems = tempItems.OrderByDescending(i => i.Priority).ThenBy(i => i.EnqueuedAt);
                    foreach (var item in sortedItems)
                    {
                        _processingQueue.Enqueue(item);
                    }
                }
                else
                {
                    _processingQueue.Enqueue(queueItem);
                }
            }
            finally
            {
                _queueSemaphore.Release();
            }

            _logger.LogInformation($"Document {documentId} enqueued with ID {queueId} and priority {priority}");
            return queueId;
        }

        public Task<ProcessingQueueStatus> GetQueueStatusAsync(Guid queueId)
        {
            if (_queueItems.TryGetValue(queueId, out var status))
            {
                return Task.FromResult(status);
            }

            throw new KeyNotFoundException($"Queue item with ID {queueId} not found");
        }

        public Task CancelProcessingAsync(Guid queueId)
        {
            if (_queueItems.TryGetValue(queueId, out var status))
            {
                if (status.State == ProcessingState.Queued || status.State == ProcessingState.Retrying)
                {
                    status.State = ProcessingState.Cancelled;
                    status.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation($"Processing cancelled for queue item {queueId}");
                }
                else
                {
                    _logger.LogWarning($"Cannot cancel queue item {queueId} in state {status.State}");
                }
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<ProcessingQueueItem>> GetPendingItemsAsync()
        {
            var pendingItems = _processingQueue.ToList()
                .Where(item => 
                {
                    if (_queueItems.TryGetValue(item.QueueId, out var status))
                    {
                        return status.State == ProcessingState.Queued || status.State == ProcessingState.Retrying;
                    }
                    return false;
                })
                .OrderByDescending(i => i.Priority)
                .ThenBy(i => i.EnqueuedAt);

            return Task.FromResult(pendingItems.AsEnumerable());
        }

        public async Task<ProcessingQueueItem?> DequeueNextAsync()
        {
            await _queueSemaphore.WaitAsync();
            try
            {
                while (_processingQueue.TryDequeue(out var item))
                {
                    if (_queueItems.TryGetValue(item.QueueId, out var status))
                    {
                        if (status.State == ProcessingState.Queued || status.State == ProcessingState.Retrying)
                        {
                            status.State = ProcessingState.Processing;
                            status.StartedAt = DateTime.UtcNow;
                            _logger.LogInformation($"Dequeued item {item.QueueId} for processing");
                            return item;
                        }
                    }
                }
            }
            finally
            {
                _queueSemaphore.Release();
            }

            return null;
        }

        public void UpdateStatus(Guid queueId, ProcessingState newState, string? errorMessage = null)
        {
            if (_queueItems.TryGetValue(queueId, out var status))
            {
                status.State = newState;
                
                if (newState == ProcessingState.Completed || newState == ProcessingState.Failed || newState == ProcessingState.Cancelled)
                {
                    status.CompletedAt = DateTime.UtcNow;
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    status.ErrorMessage = errorMessage;
                }

                _logger.LogInformation($"Updated status for queue item {queueId} to {newState}");
            }
        }

        public async Task RequeueForRetryAsync(Guid queueId, string errorMessage)
        {
            if (_queueItems.TryGetValue(queueId, out var status))
            {
                status.State = ProcessingState.Retrying;
                status.RetryCount++;
                status.ErrorMessage = errorMessage;

                var queueItem = new ProcessingQueueItem
                {
                    QueueId = queueId,
                    DocumentId = status.DocumentId,
                    Priority = status.Priority,
                    EnqueuedAt = status.EnqueuedAt,
                    RetryCount = status.RetryCount,
                    LastError = errorMessage
                };

                await _queueSemaphore.WaitAsync();
                try
                {
                    _processingQueue.Enqueue(queueItem);
                }
                finally
                {
                    _queueSemaphore.Release();
                }

                _logger.LogInformation($"Requeued item {queueId} for retry (attempt {status.RetryCount})");
            }
        }
    }
}