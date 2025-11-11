using System;
using System.Threading.Tasks;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IDocumentRepository Documents { get; }

        Task<int> SaveChangesAsync();
    }
}
