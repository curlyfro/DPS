using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentProcessor.Infrastructure.BackgroundTasks
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger<QueuedHostedService> _logger;

        public QueuedHostedService(
            IBackgroundTaskQueue taskQueue,
            ILogger<QueuedHostedService> logger)
        {
            _taskQueue = taskQueue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                if (workItem != null)
                {
                    try
                    {
                        await workItem(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Prevent throwing if stoppingToken was signaled
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred executing background work item.");
                    }
                }
            }

            _logger.LogInformation("Queued Hosted Service is stopping.");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");
            await base.StopAsync(cancellationToken);
        }
    }

    public class DocumentProcessingHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger<DocumentProcessingHostedService> _logger;
        private readonly int _maxConcurrency;
        private readonly SemaphoreSlim _semaphore;

        public DocumentProcessingHostedService(
            IBackgroundTaskQueue taskQueue,
            ILogger<DocumentProcessingHostedService> logger,
            int maxConcurrency = 3)
        {
            _taskQueue = taskQueue;
            _logger = logger;
            _maxConcurrency = maxConcurrency;
            _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Document Processing Hosted Service is starting with max concurrency: {MaxConcurrency}", 
                _maxConcurrency);

            var tasks = new Task[_maxConcurrency];
            
            for (int i = 0; i < _maxConcurrency; i++)
            {
                tasks[i] = ProcessTasksAsync(i, stoppingToken);
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation("Document Processing Hosted Service is stopping.");
        }

        private async Task ProcessTasksAsync(int workerId, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker {WorkerId} started", workerId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _semaphore.WaitAsync(stoppingToken);
                    
                    try
                    {
                        var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                        if (workItem != null)
                        {
                            _logger.LogInformation("Worker {WorkerId} processing task", workerId);
                            
                            try
                            {
                                await workItem(stoppingToken);
                                _logger.LogInformation("Worker {WorkerId} completed task", workerId);
                            }
                            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                            {
                                // Expected when cancellation is requested
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Worker {WorkerId} encountered error processing task", 
                                    workerId);
                            }
                        }
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {WorkerId} encountered unexpected error", workerId);
                    await Task.Delay(1000, stoppingToken); // Brief delay before retry
                }
            }

            _logger.LogInformation("Worker {WorkerId} stopped", workerId);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Document Processing Hosted Service is stopping.");
            await base.StopAsync(cancellationToken);
        }
    }
}