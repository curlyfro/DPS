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
    /// <summary>
    /// Repository for Document entities using Entity Framework Core queries.
    /// </summary>
    public class DocumentRepository : RepositoryBase<Document>, IDocumentRepository
    {
        new ApplicationDbContext _context;

        public DocumentRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a document by ID with loaded navigation properties.
        /// </summary>
        /// <param name="id">The unique identifier of the document</param>
        /// <returns>The document with loaded navigation properties, or null if not found</returns>
        public override async Task<Document?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        /// <summary>
        /// Retrieves all active documents with loaded navigation properties.
        /// </summary>
        /// <returns>All active documents with their document types loaded</returns>
        public new async Task<IEnumerable<Document>> GetAllAsync()
        {
            return await _dbSet
                .Where(d => !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves documents by their processing status using Entity Framework.
        /// </summary>
        /// <param name="status">The document processing status to filter by</param>
        /// <returns>Documents with the specified status, including navigation properties</returns>
        public async Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status)
        {
            return await _dbSet
                .Where(d => d.Status == status)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves documents by document type name using Entity Framework.
        /// </summary>
        /// <param name="documentTypeName">The document type name to filter by</param>
        /// <returns>Documents of the specified type</returns>
        public async Task<IEnumerable<Document>> GetByDocumentTypeAsync(string documentTypeName)
        {
            return await _dbSet
                .Where(d => d.DocumentTypeName == documentTypeName)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Document>> GetPendingDocumentsAsync(int limit = 100)
        {
            return await _dbSet
                .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Queued)
                .OrderBy(d => d.UploadedAt)
                .Take(limit)
                .ToListAsync();
        }

        public new async Task<IEnumerable<Document>> FindAsync(Expression<Func<Document, bool>> predicate)
        {
            return await _dbSet
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
            await _context.SaveChangesAsync(); // Save to database
            return document;
        }

        public new async Task<Document> UpdateAsync(Document document)
        {
            document.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(document);
            await _context.SaveChangesAsync(); // Save to database
            return document;
        }


        /// <summary>
        /// Hard deletes a document from the database.
        /// </summary>
        /// <param name="id">The unique identifier of the document to delete</param>
        public async Task DeleteAsync(Guid id)
        {
            var document = await _dbSet.FindAsync(id);
            if (document != null)
            {
                _dbSet.Remove(document);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Soft deletes a document by marking it as deleted.
        /// </summary>
        /// <param name="id">The unique identifier of the document to soft delete</param>
        public async Task SoftDeleteAsync(Guid id)
        {
            var document = await _dbSet.FindAsync(id);
            if (document != null)
            {
                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;
                _dbSet.Update(document);
                await _context.SaveChangesAsync();
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
                .Where(d => !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

    }
}