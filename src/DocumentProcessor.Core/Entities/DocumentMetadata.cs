using System;
using System.Collections.Generic;

namespace DocumentProcessor.Core.Entities
{
    public class DocumentMetadata
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public string? Author { get; set; }
        public string? Title { get; set; }
        public string? Subject { get; set; }
        public string? Keywords { get; set; }
        public DateTime? CreationDate { get; set; }
        public DateTime? ModificationDate { get; set; }
        public int? PageCount { get; set; }
        public int? WordCount { get; set; }
        public string? Language { get; set; }
        public string? Tags { get; set; } // JSON string for EF Core 8 compatibility
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}