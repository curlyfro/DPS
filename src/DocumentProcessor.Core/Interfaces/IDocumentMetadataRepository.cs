using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IDocumentMetadataRepository
    {
        Task<DocumentMetadata?> GetByIdAsync(Guid id);
        Task<DocumentMetadata?> GetByDocumentIdAsync(Guid documentId);
        Task<DocumentMetadata> AddAsync(DocumentMetadata metadata);
        Task<DocumentMetadata> UpdateAsync(DocumentMetadata metadata);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<DocumentMetadata>> GetAllAsync();
        Task<IEnumerable<DocumentMetadata>> SearchByKeywordsAsync(string keywords);
        Task<IEnumerable<DocumentMetadata>> GetByAuthorAsync(string author);
        Task<IEnumerable<DocumentMetadata>> GetByLanguageAsync(string language);
        Task<bool> AddTagAsync(Guid metadataId, string key, string value);
        Task<bool> RemoveTagAsync(Guid metadataId, string key);
    }
}
