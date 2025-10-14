using System;
using System.Threading.Tasks;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocumentProcessor.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction? _transaction;

        private IDocumentRepository? _documents;
        private IDocumentTypeRepository? _documentTypes;
        private IClassificationRepository? _classifications;
        private IProcessingQueueRepository? _processingQueues;
        private IDocumentMetadataRepository? _documentMetadata;

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

        public IDocumentTypeRepository DocumentTypes
        {
            get
            {
                _documentTypes ??= new DocumentTypeRepository(_context);
                return _documentTypes;
            }
        }

        public IClassificationRepository Classifications
        {
            get
            {
                _classifications ??= new ClassificationRepository(_context);
                return _classifications;
            }
        }

        public IProcessingQueueRepository ProcessingQueues
        {
            get
            {
                _processingQueues ??= new ProcessingQueueRepository(_context);
                return _processingQueues;
            }
        }

        public IDocumentMetadataRepository DocumentMetadata
        {
            get
            {
                _documentMetadata ??= new DocumentMetadataRepository(_context);
                return _documentMetadata;
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}
