using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IProcessingQueueRepository
    {
        Task<ProcessingQueue?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProcessingQueue>> GetPendingAsync(int limit = 100);
        Task<IEnumerable<ProcessingQueue>> GetByStatusAsync(ProcessingStatus status);
        Task<IEnumerable<ProcessingQueue>> GetByDocumentIdAsync(Guid documentId);
        Task<ProcessingQueue> AddAsync(ProcessingQueue item);
        Task<ProcessingQueue> UpdateAsync(ProcessingQueue item);
        Task DeleteAsync(Guid id);
        Task<int> GetQueueLengthAsync();
        Task<bool> StartProcessingAsync(Guid id, string processorId);
        Task<bool> CompleteProcessingAsync(Guid id, string? resultData = null);
        Task<bool> FailProcessingAsync(Guid id, string errorMessage, string? errorDetails = null);
        Task<Dictionary<ProcessingStatus, int>> GetStatusCountsAsync();
    }
}
