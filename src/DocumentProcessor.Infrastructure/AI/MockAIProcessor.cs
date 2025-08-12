using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using System.Diagnostics;

namespace DocumentProcessor.Infrastructure.AI
{
    public class MockAIProcessor : IAIProcessor
    {
        private readonly Random _random = new();
        public string ModelId => "mock-model-v1";
        public string ProviderName => "Mock AI Provider";

        public async Task<DocumentClassificationResult> ClassifyDocumentAsync(Document document, Stream documentContent)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate processing delay
            await Task.Delay(500 + _random.Next(1000));

            var categories = new[] { "Invoice", "Contract", "Report", "Letter", "Resume", "Legal", "Financial", "Technical" };
            var primaryCategory = categories[_random.Next(categories.Length)];
            
            var confidences = new Dictionary<string, double>();
            foreach (var category in categories)
            {
                confidences[category] = category == primaryCategory 
                    ? 0.85 + _random.NextDouble() * 0.14  // Primary category gets 85-99% confidence
                    : _random.NextDouble() * 0.5;         // Others get 0-50%
            }

            var tags = GenerateRandomTags(primaryCategory);

            return new DocumentClassificationResult
            {
                PrimaryCategory = primaryCategory,
                CategoryConfidences = confidences,
                Tags = tags,
                ProcessingNotes = $"Mock classification performed for document: {document.FileName}",
                ProcessingTime = stopwatch.Elapsed
            };
        }

        public async Task<DocumentExtractionResult> ExtractDataAsync(Document document, Stream documentContent)
        {
            var stopwatch = Stopwatch.StartNew();
            
            await Task.Delay(700 + _random.Next(800));

            var extractedData = new Dictionary<string, object>
            {
                ["documentId"] = document.Id,
                ["fileName"] = document.FileName,
                ["processedDate"] = DateTime.UtcNow,
                ["documentType"] = document.ContentType,
                ["pageCount"] = _random.Next(1, 20)
            };

            // Simulate entity extraction
            var entities = new List<ExtractedEntity>();
            
            // Add mock entities based on file type
            if (document.ContentType?.Contains("pdf") == true || document.ContentType?.Contains("doc") == true)
            {
                entities.Add(new ExtractedEntity
                {
                    Type = "DATE",
                    Value = DateTime.Now.AddDays(-_random.Next(30)).ToString("yyyy-MM-dd"),
                    Confidence = 0.85 + _random.NextDouble() * 0.14,
                    StartPosition = 10,
                    EndPosition = 20
                });

                entities.Add(new ExtractedEntity
                {
                    Type = "ORGANIZATION",
                    Value = "Mock Corporation Inc.",
                    Confidence = 0.90 + _random.NextDouble() * 0.09,
                    StartPosition = 50,
                    EndPosition = 72
                });

                entities.Add(new ExtractedEntity
                {
                    Type = "MONEY",
                    Value = $"${_random.Next(100, 10000)}.{_random.Next(100):D2}",
                    Confidence = 0.88 + _random.NextDouble() * 0.11,
                    StartPosition = 100,
                    EndPosition = 110
                });
            }

            return new DocumentExtractionResult
            {
                ExtractedData = extractedData,
                Entities = entities,
                Metadata = new Dictionary<string, string>
                {
                    ["extractionMethod"] = "Mock OCR + NLP",
                    ["confidence"] = (0.85 + _random.NextDouble() * 0.14).ToString("F2")
                },
                ProcessingTime = stopwatch.Elapsed
            };
        }

        public async Task<DocumentSummaryResult> GenerateSummaryAsync(Document document, Stream documentContent)
        {
            var stopwatch = Stopwatch.StartNew();
            
            await Task.Delay(600 + _random.Next(900));

            var summaryTemplates = new[]
            {
                "This document appears to be a {0} containing important information about {1}.",
                "The main purpose of this {0} is to provide details regarding {1}.",
                "This {0} document outlines key aspects of {1} and related matters."
            };

            var topics = new[] { "business operations", "financial transactions", "legal agreements", "technical specifications", "project requirements" };
            
            var summary = string.Format(
                summaryTemplates[_random.Next(summaryTemplates.Length)],
                document.ContentType?.Split('/').Last() ?? "document",
                topics[_random.Next(topics.Length)]
            );

            var keyPoints = new List<string>
            {
                $"Document created on {document.CreatedAt:yyyy-MM-dd}",
                $"File size: {document.FileSize:N0} bytes",
                $"Contains {_random.Next(3, 10)} main sections",
                $"Estimated reading time: {_random.Next(2, 15)} minutes"
            };

            var actionItems = new List<string>();
            if (_random.Next(100) > 50)
            {
                actionItems.Add("Review and approve the document");
                actionItems.Add("Share with relevant stakeholders");
                if (_random.Next(100) > 70)
                {
                    actionItems.Add("Schedule follow-up meeting");
                }
            }

            return new DocumentSummaryResult
            {
                Summary = summary,
                KeyPoints = keyPoints,
                ActionItems = actionItems,
                Language = "en",
                ProcessingTime = stopwatch.Elapsed
            };
        }

        public async Task<DocumentIntentResult> DetectIntentAsync(Document document, Stream documentContent)
        {
            var stopwatch = Stopwatch.StartNew();
            
            await Task.Delay(400 + _random.Next(600));

            var intents = new[]
            {
                ("RequestApproval", "Route to approval workflow"),
                ("InformationSharing", "Distribute to team members"),
                ("RecordKeeping", "Archive in document management system"),
                ("ActionRequired", "Add to task queue for processing"),
                ("FYI", "Mark as read and file")
            };

            var selectedIntent = intents[_random.Next(intents.Length)];
            var secondaryIntents = intents
                .Where(i => i.Item1 != selectedIntent.Item1)
                .Take(_random.Next(1, 3))
                .Select(i => i.Item1)
                .ToList();

            var parameters = new Dictionary<string, string>
            {
                ["urgency"] = _random.Next(100) > 70 ? "high" : "normal",
                ["department"] = new[] { "HR", "Finance", "Legal", "IT", "Operations" }[_random.Next(5)],
                ["requiresSignature"] = _random.Next(100) > 60 ? "true" : "false"
            };

            return new DocumentIntentResult
            {
                PrimaryIntent = selectedIntent.Item1,
                SecondaryIntents = secondaryIntents,
                Confidence = 0.75 + _random.NextDouble() * 0.24,
                SuggestedAction = selectedIntent.Item2,
                Parameters = parameters,
                ProcessingTime = stopwatch.Elapsed
            };
        }

        public async Task<ProcessingCost> EstimateCostAsync(long fileSize, string contentType)
        {
            await Task.Delay(100);

            // Mock cost calculation based on file size
            var estimatedTokens = (int)(fileSize / 4); // Rough approximation: 1 token per 4 bytes
            var costPerThousandTokens = 0.002m; // Mock pricing
            var estimatedCost = (estimatedTokens / 1000m) * costPerThousandTokens;

            return new ProcessingCost
            {
                EstimatedCost = Math.Round(estimatedCost, 4),
                Currency = "USD",
                EstimatedTokens = estimatedTokens,
                ModelUsed = ModelId,
                PricingTier = estimatedTokens < 10000 ? "Basic" : estimatedTokens < 100000 ? "Standard" : "Premium"
            };
        }

        private List<string> GenerateRandomTags(string category)
        {
            var tagPool = new Dictionary<string, List<string>>
            {
                ["Invoice"] = new() { "billing", "payment", "accounting", "vendor", "purchase-order" },
                ["Contract"] = new() { "legal", "agreement", "terms", "signature-required", "binding" },
                ["Report"] = new() { "analysis", "metrics", "quarterly", "performance", "summary" },
                ["Letter"] = new() { "correspondence", "communication", "formal", "response-needed" },
                ["Resume"] = new() { "hr", "recruitment", "candidate", "employment", "skills" },
                ["Legal"] = new() { "compliance", "regulatory", "litigation", "policy", "confidential" },
                ["Financial"] = new() { "budget", "revenue", "expense", "audit", "fiscal" },
                ["Technical"] = new() { "specifications", "requirements", "architecture", "documentation", "engineering" }
            };

            var tags = new List<string>();
            if (tagPool.ContainsKey(category))
            {
                var availableTags = tagPool[category];
                var tagCount = _random.Next(2, Math.Min(5, availableTags.Count + 1));
                tags = availableTags.OrderBy(x => _random.Next()).Take(tagCount).ToList();
            }

            return tags;
        }
    }
}