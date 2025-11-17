using System;
using System.Threading.Tasks;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Repositories;

namespace DocumentProcessor.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IDocumentRepository? _documents;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IDocumentRepository Documents
        {
            get
            {
                _documents ??= new DocumentRepository(_context);
                return _documents;
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
