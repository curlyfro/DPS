using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IDocumentClassifier
    {
        Task<ClassificationResult> ClassifyDocumentAsync(Document document);
        Task<ClassificationResult> ClassifyTextAsync(string text);
        Task<IEnumerable<Intent>> DetectIntentsAsync(string text);
        Task<IEnumerable<Entity>> ExtractEntitiesAsync(string text);
        Task<DocumentType?> SuggestDocumentTypeAsync(Document document);
        Task<bool> TrainClassifierAsync(IEnumerable<TrainingData> trainingData);
    }

    public class ClassificationResult
    {
        public Guid DocumentTypeId { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public Dictionary<string, double> AlternativeClassifications { get; set; } = new();
        public IEnumerable<Intent> Intents { get; set; } = new List<Intent>();
        public IEnumerable<Entity> Entities { get; set; } = new List<Entity>();
        public string? Explanation { get; set; }
    }

    public class Intent
    {
        public string Name { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class Entity
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public double Confidence { get; set; }
    }

    public class TrainingData
    {
        public string Text { get; set; } = string.Empty;
        public Guid DocumentTypeId { get; set; }
        public List<Intent> Intents { get; set; } = new();
        public List<Entity> Entities { get; set; } = new();
    }
}