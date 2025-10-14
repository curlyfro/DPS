using System;
using System.Threading.Tasks;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IDocumentRepository Documents { get; }
        IDocumentTypeRepository DocumentTypes { get; }
        IClassificationRepository Classifications { get; }
        IProcessingQueueRepository ProcessingQueues { get; }
        IDocumentMetadataRepository DocumentMetadata { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
