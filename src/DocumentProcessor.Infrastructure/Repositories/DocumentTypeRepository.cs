using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Data;

namespace DocumentProcessor.Infrastructure.Repositories
{
    public class DocumentTypeRepository : RepositoryBase<DocumentType>, IDocumentTypeRepository
    {
        public DocumentTypeRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task<DocumentType?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(dt => dt.Documents)
                .Include(dt => dt.Classifications)
                .FirstOrDefaultAsync(dt => dt.Id == id);
        }

        public async Task<DocumentType?> GetByNameAsync(string name)
        {
            return await _dbSet
                .FirstOrDefaultAsync(dt => dt.Name.ToLower() == name.ToLower());
        }

        public new async Task<IEnumerable<DocumentType>> GetAllAsync()
        {
            var documentTypeDtos = await _context.Database.SqlQueryRaw<DocumentTypeDto>(
                "EXEC dbo.GetDocumentTypes").ToListAsync();
            
            return documentTypeDtos.Select(dto => dto.ToDocumentType()).ToList();
        }

        public async Task<IEnumerable<DocumentType>> GetActiveAsync()
        {
            return await _dbSet
                .Where(dt => dt.IsActive)
                .OrderBy(dt => dt.Priority)
                .ThenBy(dt => dt.Name)
                .ToListAsync();
        }

        public override async Task<DocumentType> AddAsync(DocumentType documentType)
        {
            if (documentType.Id == Guid.Empty)
                documentType.Id = Guid.NewGuid();

            documentType.CreatedAt = DateTime.UtcNow;
            documentType.UpdatedAt = DateTime.UtcNow;

            await _dbSet.AddAsync(documentType);
            await _context.SaveChangesAsync(); // Save to database
            return documentType;
        }

        public new async Task<DocumentType> UpdateAsync(DocumentType documentType)
        {
            documentType.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(documentType);
            await _context.SaveChangesAsync(); // Save to database
            return documentType;
        }

        public async Task DeleteAsync(Guid id)
        {
            var documentType = await _dbSet.FindAsync(id);
            if (documentType != null)
            {
                _dbSet.Remove(documentType);
                await _context.SaveChangesAsync(); // Save to database
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _dbSet.AnyAsync(dt => dt.Id == id);
        }
    }
}