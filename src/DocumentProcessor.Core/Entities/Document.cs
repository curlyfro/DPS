using System;

namespace DocumentProcessor.Core.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string? S3Key { get; set; }
        public string? S3Bucket { get; set; }
        public DocumentSource Source { get; set; }
        public DocumentStatus Status { get; set; }

        // Flattened DocumentType fields
        public string? DocumentTypeName { get; set; }
        public string? DocumentTypeCategory { get; set; }

        // Flattened Processing fields
        public string? ProcessingStatus { get; set; }
        public int ProcessingRetryCount { get; set; }
        public string? ProcessingErrorMessage { get; set; }
        public DateTime? ProcessingStartedAt { get; set; }
        public DateTime? ProcessingCompletedAt { get; set; }

        // Content fields
        public string? ExtractedText { get; set; }
        public string? Summary { get; set; }

        // Audit fields
        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public enum DocumentSource
    {
        LocalUpload,
        S3,
        FileShare
    }

    public enum DocumentStatus
    {
        Pending,
        Queued,
        Processing,
        Processed,
        Failed
    }
}