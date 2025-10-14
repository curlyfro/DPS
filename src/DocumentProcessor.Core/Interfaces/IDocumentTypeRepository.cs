using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IDocumentTypeRepository
    {
        Task<DocumentType?> GetByIdAsync(Guid id);
        Task<DocumentType?> GetByNameAsync(string name);
        Task<IEnumerable<DocumentType>> GetAllAsync();
        Task<IEnumerable<DocumentType>> GetActiveAsync();
        Task<DocumentType> AddAsync(DocumentType documentType);
        Task<DocumentType> UpdateAsync(DocumentType documentType);
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
    }
}
