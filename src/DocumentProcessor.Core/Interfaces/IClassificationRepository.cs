using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IClassificationRepository
    {
        Task<Classification?> GetByIdAsync(Guid id);
        Task<IEnumerable<Classification>> GetByDocumentIdAsync(Guid documentId);
        Task<IEnumerable<Classification>> GetByDocumentTypeIdAsync(Guid documentTypeId);
        Task<Classification> AddAsync(Classification classification);
        Task<Classification> UpdateAsync(Classification classification);
        Task DeleteAsync(Guid id);
    }
}
