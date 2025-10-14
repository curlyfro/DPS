using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IAIProcessor
    {
        Task<DocumentClassificationResult> ClassifyDocumentAsync(Document document, Stream documentContent);
        Task<DocumentSummaryResult> GenerateSummaryAsync(Document document, Stream documentContent);
        string ModelId { get; }
        string ProviderName { get; }
    }

    public class DocumentClassificationResult
    {
        public string PrimaryCategory { get; set; } = string.Empty;
        public Dictionary<string, double> CategoryConfidences { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string ProcessingNotes { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
    }

    public class DocumentSummaryResult
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new();
        public List<string> ActionItems { get; set; } = new();
        public string Language { get; set; } = "en";
        public TimeSpan ProcessingTime { get; set; }
    }
}