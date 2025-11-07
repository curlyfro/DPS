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
    public class ClassificationRepository : RepositoryBase<Classification>, IClassificationRepository
    {
        public ClassificationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task<Classification?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(c => c.Document)
                .Include(c => c.DocumentType)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<IEnumerable<Classification>> GetByDocumentIdAsync(Guid documentId)
        {
            return await _dbSet
                .Include(c => c.DocumentType)
                .Where(c => c.DocumentId == documentId)
                .OrderByDescending(c => c.ConfidenceScore)
                .ToListAsync();
        }

        public override async Task<Classification> AddAsync(Classification classification)
        {
            if (classification.Id == Guid.Empty)
                classification.Id = Guid.NewGuid();

            classification.ClassifiedAt = DateTime.UtcNow;
            classification.CreatedAt = DateTime.UtcNow;
            classification.UpdatedAt = DateTime.UtcNow;

            await _dbSet.AddAsync(classification);
            await _context.SaveChangesAsync(); // Save to database
            return classification;
        }

        public new async Task<Classification> UpdateAsync(Classification classification)
        {
            classification.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(classification);
            await _context.SaveChangesAsync(); // Save to database
            return classification;
        }

        public async Task DeleteAsync(Guid id)
        {
            var classification = await _dbSet.FindAsync(id);
            if (classification != null)
            {
                _dbSet.Remove(classification);
                await _context.SaveChangesAsync(); // Save to database
            }
        }
    }
}