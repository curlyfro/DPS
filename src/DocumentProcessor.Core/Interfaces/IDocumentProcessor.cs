using System;
using System.IO;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IDocumentProcessor
    {
        Task<ProcessingResult> ProcessDocumentAsync(Document document);
        Task<string> ExtractTextAsync(Document document);
        Task<string> ExtractTextFromStreamAsync(Stream stream, string contentType);
        Task<DocumentSummary> GenerateSummaryAsync(Document document);
        Task<bool> ValidateDocumentAsync(Document document);
        Task<ProcessingResult> ReprocessDocumentAsync(Guid documentId);
    }

    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string? ExtractedText { get; set; }
        public string? Summary { get; set; }
        public Classification? Classification { get; set; }
        public string? ErrorMessage { get; set; }
        public ProcessingMetrics? Metrics { get; set; }
    }

    public class DocumentSummary
    {
        public string Summary { get; set; } = string.Empty;
        public string[] KeyPoints { get; set; } = Array.Empty<string>();
        public int WordCount { get; set; }
        public string Language { get; set; } = "en";
    }

    public class ProcessingMetrics
    {
        public TimeSpan ProcessingTime { get; set; }
        public int TokensUsed { get; set; }
        public decimal EstimatedCost { get; set; }
        public string ModelUsed { get; set; } = string.Empty;
    }
}