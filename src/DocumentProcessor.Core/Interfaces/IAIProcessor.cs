using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IAIProcessor
    {
        string ModelName { get; }
        Task<AIResponse> ProcessAsync(AIRequest request);
        Task<string> GenerateTextAsync(string prompt, AIModelOptions? options = null);
        Task<string> SummarizeTextAsync(string text, int maxLength = 500);
        Task<string> TranslateTextAsync(string text, string targetLanguage);
        Task<IEnumerable<string>> ExtractKeyPhrasesAsync(string text);
        Task<double> AnalyzeSentimentAsync(string text);
        Task<bool> IsModelAvailableAsync();
        Task<AIModelInfo> GetModelInfoAsync();
    }

    public class AIRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? SystemPrompt { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public AIModelOptions? Options { get; set; }
        public string? Context { get; set; }
    }

    public class AIResponse
    {
        public string Content { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int TokensUsed { get; set; }
        public decimal EstimatedCost { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class AIModelOptions
    {
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 1000;
        public double TopP { get; set; } = 0.9;
        public int? Seed { get; set; }
        public List<string> StopSequences { get; set; } = new();
    }

    public class AIModelInfo
    {
        public string ModelId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int MaxInputTokens { get; set; }
        public int MaxOutputTokens { get; set; }
        public decimal CostPerThousandTokens { get; set; }
        public List<string> SupportedLanguages { get; set; } = new();
        public List<string> Capabilities { get; set; } = new();
    }
}