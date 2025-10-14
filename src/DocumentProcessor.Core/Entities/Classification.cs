using System;

namespace DocumentProcessor.Core.Entities
{
    public class Classification
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public Guid DocumentTypeId { get; set; }
        public DocumentType DocumentType { get; set; } = null!;
        public double ConfidenceScore { get; set; }
        public ClassificationMethod Method { get; set; }
        public string? AIModelUsed { get; set; }
        public string? AIResponse { get; set; }
        public bool IsManuallyVerified { get; set; }
        public DateTime ClassifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public enum ClassificationMethod
    {
        AI
    }
}