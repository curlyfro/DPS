using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Data;

namespace DocumentProcessor.Infrastructure.Repositories
{
    public class DocumentRepository : RepositoryBase<Document>, IDocumentRepository
    {
        public DocumentRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Document?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Include(d => d.Classifications)
                    .ThenInclude(c => c.DocumentType)
                .Include(d => d.ProcessingQueueItems)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public new async Task<IEnumerable<Document>> GetAllAsync()
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(d => d.Status == status)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetByDocumentTypeAsync(Guid documentTypeId)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(d => d.DocumentTypeId == documentTypeId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetByUserAsync(string userId)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(d => d.UploadedBy == userId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetPendingDocumentsAsync(int limit = 100)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Queued)
                .OrderBy(d => d.UploadedAt)
                .Take(limit)
                .ToListAsync();
        }

        public new async Task<IEnumerable<Document>> FindAsync(Expression<Func<Document, bool>> predicate)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(predicate)
                .ToListAsync();
        }

        public new async Task<Document> AddAsync(Document document)
        {
            if (document.Id == Guid.Empty)
                document.Id = Guid.NewGuid();
            
            document.CreatedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            
            await _dbSet.AddAsync(document);
            return document;
        }

        public async Task<Document> UpdateAsync(Document document)
        {
            document.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(document);
            return document;
        }

        public async Task DeleteAsync(Guid id)
        {
            var document = await _dbSet.FindAsync(id);
            if (document != null)
            {
                _dbSet.Remove(document);
            }
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            var document = await _dbSet.FindAsync(id);
            if (document != null)
            {
                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;
                _dbSet.Update(document);
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _dbSet.AnyAsync(d => d.Id == id);
        }

        public new async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public new async Task<int> CountAsync(Expression<Func<Document, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }

        public new async Task<IEnumerable<Document>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .OrderByDescending(d => d.UploadedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetRecentDocumentsAsync(int days = 7, int limit = 100)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(d => d.UploadedAt >= cutoffDate)
                .OrderByDescending(d => d.UploadedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Dictionary<DocumentStatus, int>> GetStatusCountsAsync()
        {
            return await _dbSet
                .GroupBy(d => d.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<Dictionary<string, int>> GetDocumentTypeCountsAsync()
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Where(d => d.DocumentType != null)
                .GroupBy(d => d.DocumentType!.Name)
                .Select(g => new { TypeName = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TypeName, x => x.Count);
        }
    }
}