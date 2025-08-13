using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentProcessor.Infrastructure.AI
{
    /// <summary>
    /// Amazon Bedrock implementation of the AI processor
    /// </summary>
    public class BedrockAIProcessor : IAIProcessor
    {
        private readonly ILogger<BedrockAIProcessor> _logger;
        private readonly BedrockOptions _options;
        private readonly IAmazonBedrockRuntime _bedrockClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly DocumentContentExtractor _contentExtractor;

        public string ModelId => _options.ClassificationModelId;
        public string ProviderName => "Amazon Bedrock";

        public BedrockAIProcessor(
            ILogger<BedrockAIProcessor> logger,
            IOptions<BedrockOptions> options,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _options = options.Value;
            
            // Try to get DocumentContentExtractor from DI, or create a new instance
            var contentExtractorLogger = serviceProvider.GetService<ILogger<DocumentContentExtractor>>();
            _contentExtractor = serviceProvider.GetService<DocumentContentExtractor>() ??
                new DocumentContentExtractor(contentExtractorLogger ??
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentContentExtractor>());
            
            // Initialize Bedrock client
            var config = new AmazonBedrockRuntimeConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region)
            };

            // Use profile if specified (for development)
            if (!string.IsNullOrEmpty(_options.AwsProfile))
            {
                var credentials = new Amazon.Runtime.StoredProfileAWSCredentials(_options.AwsProfile);
                _bedrockClient = new AmazonBedrockRuntimeClient(credentials, config);
            }
            else
            {
                // Use default credentials (IAM role, environment variables, etc.)
                _bedrockClient = new AmazonBedrockRuntimeClient(config);
            }

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        public async Task<DocumentClassificationResult> ClassifyDocumentAsync(
            Document document, 
            Stream documentContent)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Classifying document {DocumentId} using Bedrock model: {Model}", 
                    document.Id, _options.ClassificationModelId);

                // Extract content using the enhanced extractor
                var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
                string content = FormatExtractedContent(extractedContent);
                
                var prompt = BuildClassificationPrompt(content, document.FileName);
                var response = await InvokeModelAsync(
                    _options.ClassificationModelId, 
                    prompt, 
                    CancellationToken.None);

                var result = ParseClassificationResponse(response);
                result.ProcessingTime = DateTime.UtcNow - startTime;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying document {DocumentId} with Bedrock", document.Id);
                return new DocumentClassificationResult
                {
                    PrimaryCategory = "Unknown",
                    ProcessingNotes = $"Error: {ex.Message}",
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<DocumentExtractionResult> ExtractDataAsync(
            Document document, 
            Stream documentContent)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Extracting data from document {DocumentId} using Bedrock model: {Model}", 
                    document.Id, _options.ExtractionModelId);

                var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
                string content = FormatExtractedContent(extractedContent);
                
                var prompt = BuildExtractionPrompt(content, document.DocumentType?.Name ?? "Document");
                var response = await InvokeModelAsync(
                    _options.ExtractionModelId, 
                    prompt, 
                    CancellationToken.None);

                var result = ParseExtractionResponse(response);
                result.ProcessingTime = DateTime.UtcNow - startTime;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting data from document {DocumentId} with Bedrock", document.Id);
                return new DocumentExtractionResult
                {
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<DocumentSummaryResult> GenerateSummaryAsync(
            Document document,
            Stream documentContent)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Summarizing document {DocumentId} using Bedrock model: {Model}",
                    document.Id, _options.SummarizationModelId);

                var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
                string content = FormatExtractedContent(extractedContent);
                
                // Pass document info for better mock generation
                var prompt = BuildSummarizationPrompt(content, 500, document.FileName, document.Id.ToString());
                var response = await InvokeModelAsync(
                    _options.SummarizationModelId,
                    prompt,
                    CancellationToken.None);

                var result = ParseSummaryResponse(response);
                result.ProcessingTime = DateTime.UtcNow - startTime;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing document {DocumentId} with Bedrock", document.Id);
                return new DocumentSummaryResult
                {
                    Summary = "Error generating summary",
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<DocumentIntentResult> DetectIntentAsync(
            Document document, 
            Stream documentContent)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Detecting intent for document {DocumentId} using Bedrock model: {Model}", 
                    document.Id, _options.IntentModelId);

                var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
                string content = FormatExtractedContent(extractedContent);
                
                var prompt = BuildIntentPrompt(content);
                var response = await InvokeModelAsync(
                    _options.IntentModelId, 
                    prompt, 
                    CancellationToken.None);

                var result = ParseIntentResponse(response);
                result.ProcessingTime = DateTime.UtcNow - startTime;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting intent for document {DocumentId} with Bedrock", document.Id);
                return new DocumentIntentResult
                {
                    PrimaryIntent = "Unknown",
                    Confidence = 0,
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<ProcessingCost> EstimateCostAsync(long fileSize, string contentType)
        {
            // Estimate tokens based on file size (rough approximation)
            // Assuming ~4 characters per token and some overhead for prompts
            var estimatedCharacters = fileSize * 0.8; // Assuming 80% of file is text
            var estimatedTokens = (int)(estimatedCharacters / 4) + 500; // Add 500 for prompt overhead
            
            // Claude 3 Haiku pricing: $0.25 per million input tokens, $1.25 per million output tokens
            // Using average for estimation
            var costPerMillionTokens = 0.75m;
            var estimatedCost = (estimatedTokens / 1000000m) * costPerMillionTokens;
            
            return await Task.FromResult(new ProcessingCost
            {
                EstimatedCost = estimatedCost,
                Currency = "USD",
                EstimatedTokens = estimatedTokens,
                ModelUsed = _options.ClassificationModelId,
                PricingTier = "Pay-as-you-go"
            });
        }

        private async Task<string> ReadStreamContent(Stream stream)
        {
            // Reset stream position if possible
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            
            // Truncate content if it's too long for the model
            // Claude models have different token limits, using conservative character limit
            const int maxCharacters = 50000; // Approximately 12,500 tokens
            if (content.Length > maxCharacters)
            {
                _logger.LogWarning($"Document content truncated from {content.Length} to {maxCharacters} characters");
                content = content.Substring(0, maxCharacters) + "\n\n[Content truncated due to length...]";
            }
            
            return content;
        }

        private async Task<string> InvokeModelAsync(
            string modelId,
            string prompt,
            CancellationToken cancellationToken)
        {
            // Check if we should use simulated responses
            if (_options.UseSimulatedResponses)
            {
                _logger.LogInformation("Using simulated response for model {ModelId}", modelId);
                await Task.Delay(100, cancellationToken); // Simulate processing time
                return GenerateSimulatedResponse(prompt, modelId);
            }

            var retryCount = 0;
            var delay = _options.RetryDelayMilliseconds;

            while (retryCount < _options.MaxRetries)
            {
                try
                {
                    // Build the request body for Claude models
                    var requestBody = new
                    {
                        anthropic_version = "bedrock-2023-05-31",
                        max_tokens = _options.MaxTokens,
                        messages = new[]
                        {
                            new
                            {
                                role = "user",
                                content = prompt
                            }
                        },
                        temperature = _options.Temperature,
                        top_p = _options.TopP
                    };

                    var request = new InvokeModelRequest
                    {
                        ModelId = modelId,
                        Body = new MemoryStream(Encoding.UTF8.GetBytes(
                            JsonSerializer.Serialize(requestBody, _jsonOptions))),
                        ContentType = "application/json",
                        Accept = "application/json"
                    };

                    if (_options.EnableDetailedLogging)
                    {
                        _logger.LogDebug("Bedrock request: {Request}",
                            JsonSerializer.Serialize(requestBody, _jsonOptions));
                    }

                    var response = await _bedrockClient.InvokeModelAsync(request, cancellationToken);
                    
                    using var reader = new StreamReader(response.Body);
                    var responseBody = await reader.ReadToEndAsync();

                    if (_options.EnableDetailedLogging)
                    {
                        _logger.LogDebug("Bedrock response: {Response}", responseBody);
                    }

                    // Parse Claude response format
                    var responseJson = JsonDocument.Parse(responseBody);
                    if (responseJson.RootElement.TryGetProperty("content", out var content))
                    {
                        var textContent = content[0].GetProperty("text").GetString();
                        return textContent ?? string.Empty;
                    }

                    return responseBody;
                }
                catch (AmazonBedrockRuntimeException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                    ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    retryCount++;
                    if (retryCount >= _options.MaxRetries)
                    {
                        throw;
                    }

                    _logger.LogWarning("Bedrock API throttled, retrying in {Delay}ms", delay);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2; // Exponential backoff
                }
            }

            throw new InvalidOperationException($"Failed to invoke Bedrock model after {_options.MaxRetries} retries");
        }

        private string GenerateSimulatedResponse(string prompt, string modelId)
        {
            // Generate varied simulated responses based on the prompt content
            var promptLower = prompt.ToLowerInvariant();
            
            // For classification
            if (promptLower.Contains("classify"))
            {
                var categories = new[] { "Invoice", "Contract", "Report", "Letter", "Memo", "Receipt", "Form", "Agreement" };
                var tags = new[] { "financial", "legal", "administrative", "technical", "urgent", "confidential", "internal", "external" };
                var random = new Random(Guid.NewGuid().GetHashCode());
                
                var selectedCategory = categories[random.Next(categories.Length)];
                var selectedTags = tags.OrderBy(x => random.Next()).Take(random.Next(2, 4)).ToArray();
                
                return JsonSerializer.Serialize(new
                {
                    category = selectedCategory,
                    confidence = Math.Round(0.7 + random.NextDouble() * 0.3, 2),
                    tags = selectedTags
                });
            }
            
            // For entity extraction
            if (promptLower.Contains("extract"))
            {
                var random = new Random(Guid.NewGuid().GetHashCode());
                var entities = new List<object>();
                
                // Generate varied entity types and values
                var entityTypes = new[] { "DATE", "PERSON", "ORGANIZATION", "AMOUNT", "LOCATION", "REFERENCE_NUMBER", "EMAIL", "PHONE" };
                var sampleValues = new Dictionary<string, string[]>
                {
                    ["DATE"] = new[] { "2024-03-15", "January 5, 2024", "Q3 2024", "2024-06-30", "Next Monday" },
                    ["PERSON"] = new[] { "John Smith", "Jane Doe", "Robert Johnson", "Maria Garcia", "David Chen" },
                    ["ORGANIZATION"] = new[] { "Acme Corporation", "Global Tech Inc", "Smith & Associates", "National Bank", "City University" },
                    ["AMOUNT"] = new[] { "$1,234.56", "$50,000", "€10,500.00", "$789.99", "$2,500,000" },
                    ["LOCATION"] = new[] { "New York, NY", "San Francisco", "123 Main Street", "Building A, Room 205", "London, UK" },
                    ["REFERENCE_NUMBER"] = new[] { "INV-2024-001", "PO-98765", "REF-ABC123", "DOC-456789", "CASE-2024-789" },
                    ["EMAIL"] = new[] { "contact@example.com", "info@company.org", "support@service.net", "admin@domain.com" },
                    ["PHONE"] = new[] { "(555) 123-4567", "+1-800-555-0123", "555.987.6543", "(212) 555-1234" }
                };
                
                var selectedTypes = entityTypes.OrderBy(x => random.Next()).Take(random.Next(3, 6));
                foreach (var type in selectedTypes)
                {
                    if (sampleValues.ContainsKey(type))
                    {
                        var values = sampleValues[type];
                        entities.Add(new
                        {
                            type = type,
                            value = values[random.Next(values.Length)],
                            confidence = Math.Round(0.75 + random.NextDouble() * 0.25, 2)
                        });
                    }
                }
                
                return JsonSerializer.Serialize(new { entities });
            }
            
            // For summarization
            if (promptLower.Contains("summary") || promptLower.Contains("summarize"))
            {
                // Extract document info from prompt if available
                var docId = ExtractDocumentId(prompt);
                var fileName = ExtractFileName(prompt);
                
                var summaryTemplates = new[]
                {
                    "This document outlines the key requirements and specifications for the upcoming project implementation. It details the timeline, resource allocation, and expected deliverables. The main focus is on ensuring compliance with regulatory standards while maintaining operational efficiency. Key stakeholders have been identified and their responsibilities clearly defined.",
                    "The report presents a comprehensive analysis of current market trends and their impact on business operations. Financial projections indicate positive growth potential over the next quarter. Several strategic recommendations are provided to capitalize on emerging opportunities. Risk mitigation strategies have been thoroughly evaluated.",
                    "This agreement establishes the terms and conditions for the professional services engagement. It specifies the scope of work, payment terms, and performance metrics. Both parties' obligations are clearly outlined with specific deadlines. Confidentiality and intellectual property clauses are included.",
                    "The document contains important policy updates affecting all organizational departments. Implementation procedures are detailed with step-by-step guidelines. Training requirements and compliance deadlines are specified. Management approval processes have been streamlined for efficiency.",
                    "This correspondence addresses recent inquiries regarding operational procedures. Clarifications are provided on multiple policy points raised by stakeholders. Action items are prioritized based on urgency and impact. Follow-up meetings have been scheduled to ensure proper implementation."
                };
                
                // Use document ID to consistently select a summary (but make it appear random)
                var hash = string.IsNullOrEmpty(docId) ? Guid.NewGuid().GetHashCode() : docId.GetHashCode();
                var index = Math.Abs(hash) % summaryTemplates.Length;
                var baseSummary = summaryTemplates[index];
                
                // Add document-specific context if available
                if (!string.IsNullOrEmpty(fileName))
                {
                    var fileType = System.IO.Path.GetExtension(fileName)?.ToLower() ?? "";
                    if (fileType == ".pdf")
                        baseSummary = $"PDF document analysis: {baseSummary}";
                    else if (fileType == ".docx" || fileType == ".doc")
                        baseSummary = $"Word document review: {baseSummary}";
                    else if (fileType == ".txt")
                        baseSummary = $"Text file content: {baseSummary}";
                }
                
                // Add unique timestamp to make each summary slightly different
                var timestamp = DateTime.UtcNow.ToString("HH:mm");
                return $"{baseSummary} (Processed at {timestamp} UTC)";
            }
            
            // For intent detection
            if (promptLower.Contains("intent") || promptLower.Contains("purpose"))
            {
                var intents = new[]
                {
                    new { intent = "Information Request", action = "Provide requested information" },
                    new { intent = "Approval Required", action = "Review and approve" },
                    new { intent = "Payment Request", action = "Process payment" },
                    new { intent = "Status Update", action = "Acknowledge receipt" },
                    new { intent = "Action Required", action = "Take immediate action" },
                    new { intent = "Policy Notification", action = "Review and comply" },
                    new { intent = "Meeting Request", action = "Schedule meeting" }
                };
                
                var random = new Random(Guid.NewGuid().GetHashCode());
                var selected = intents[random.Next(intents.Length)];
                
                return JsonSerializer.Serialize(new
                {
                    primaryIntent = selected.intent,
                    confidence = Math.Round(0.7 + random.NextDouble() * 0.3, 2),
                    suggestedAction = selected.action
                });
            }
            
            // Default response
            return "Simulated response generated for testing purposes.";
        }

        private string ExtractDocumentId(string prompt)
        {
            var match = System.Text.RegularExpressions.Regex.Match(prompt, @"Document ID:\s*([a-f0-9-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "";
        }

        private string ExtractFileName(string prompt)
        {
            var match = System.Text.RegularExpressions.Regex.Match(prompt, @"Document name:\s*([^\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private string GenerateSuggestedAction(string intent)
        {
            return intent switch
            {
                "Payment Request" => "Process payment",
                "Information Request" => "Provide requested information",
                "Approval Required" => "Review and approve",
                "Status Update" => "Acknowledge receipt",
                "Action Required" => "Take immediate action",
                _ => "Review document"
            };
        }

        private string BuildClassificationPrompt(string content, string fileName)
        {
            // Content is already truncated in ReadStreamContent
            return $@"Analyze the following document and classify it.

Document name: {fileName}
Document content:
{content}

Respond with a JSON object containing:
- category: the document category (e.g., Invoice, Contract, Report, Letter, etc.)
- confidence: a number between 0 and 1
- tags: array of relevant tags

Example response:
{{""category"": ""Invoice"", ""confidence"": 0.95, ""tags"": [""financial"", ""billing""]}}";
        }

        private string BuildExtractionPrompt(string content, string documentType)
        {
            // Content is already truncated in ReadStreamContent
            return $@"Extract key entities from this {documentType} document.

Document content:
{content}

Respond with a JSON object containing an 'entities' array, where each entity has:
- type: entity type (e.g., DATE, PERSON, ORGANIZATION, AMOUNT, etc.)
- value: extracted value
- confidence: extraction confidence (0-1)

Focus on extracting relevant information for a {documentType}.";
        }

        private string BuildSummarizationPrompt(string content, int maxLength, string fileName = null, string documentId = null)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine($"Provide a concise summary of the following document in approximately {maxLength} characters.");
            
            if (!string.IsNullOrEmpty(fileName))
            {
                promptBuilder.AppendLine($"Document name: {fileName}");
            }
            
            if (!string.IsNullOrEmpty(documentId))
            {
                promptBuilder.AppendLine($"Document ID: {documentId}");
            }
            
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Document content:");
            promptBuilder.AppendLine(content);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Create a clear, informative summary that captures the main points and purpose of the document.");
            
            return promptBuilder.ToString();
        }

        private string BuildIntentPrompt(string content)
        {
            // Content is already truncated in ReadStreamContent
            return $@"Analyze the intent or purpose of this document.

Document content:
{content}

Respond with a JSON object containing:
- primaryIntent: main purpose of the document
- confidence: overall confidence (0-1)
- suggestedAction: recommended action

Example response:
{{""primaryIntent"": ""Payment Request"", ""confidence"": 0.9, ""suggestedAction"": ""Process payment""}}";
        }

        private DocumentClassificationResult ParseClassificationResponse(string response)
        {
            try
            {
                // Extract JSON from the text response
                var jsonString = ExtractJsonFromResponse(response);
                if (string.IsNullOrEmpty(jsonString))
                {
                    // Fallback: try to parse the response directly
                    jsonString = response;
                }

                var json = JsonDocument.Parse(jsonString);
                var root = json.RootElement;

                var result = new DocumentClassificationResult
                {
                    PrimaryCategory = root.TryGetProperty("category", out var cat)
                        ? cat.GetString() ?? "Unknown" : "Unknown",
                    ProcessingNotes = $"Model: {_options.ClassificationModelId}"
                };

                if (root.TryGetProperty("confidence", out var conf))
                {
                    result.CategoryConfidences[result.PrimaryCategory] = conf.GetDouble();
                }

                if (root.TryGetProperty("tags", out var tags))
                {
                    foreach (var tag in tags.EnumerateArray())
                    {
                        result.Tags.Add(tag.GetString() ?? "");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse classification response");
                return new DocumentClassificationResult
                {
                    PrimaryCategory = "Unknown",
                    ProcessingNotes = $"Parse error: {ex.Message}"
                };
            }
        }

        private DocumentExtractionResult ParseExtractionResponse(string response)
        {
            try
            {
                // Extract JSON from the text response
                var jsonString = ExtractJsonFromResponse(response);
                if (string.IsNullOrEmpty(jsonString))
                {
                    // Fallback: try to parse the response directly
                    jsonString = response;
                }

                var json = JsonDocument.Parse(jsonString);
                var root = json.RootElement;

                var result = new DocumentExtractionResult();
                
                if (root.TryGetProperty("entities", out var entitiesArray))
                {
                    foreach (var entity in entitiesArray.EnumerateArray())
                    {
                        result.Entities.Add(new ExtractedEntity
                        {
                            Type = entity.TryGetProperty("type", out var type)
                                ? type.GetString() ?? "" : "",
                            Value = entity.TryGetProperty("value", out var val)
                                ? val.GetString() ?? "" : "",
                            Confidence = entity.TryGetProperty("confidence", out var conf)
                                ? conf.GetDouble() : 0.8
                        });
                    }
                }

                result.Metadata["model"] = _options.ExtractionModelId;
                result.Metadata["entityCount"] = result.Entities.Count.ToString();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse extraction response");
                return new DocumentExtractionResult();
            }
        }

        private DocumentSummaryResult ParseSummaryResponse(string response)
        {
            // Response is plain text summary
            var result = new DocumentSummaryResult
            {
                Summary = response.Trim(),
                Language = "en"
            };

            // Extract key points from summary (simple sentence splitting)
            var sentences = response.Split(new[] { '.', '!', '?' }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var sentence in sentences.Take(5))
            {
                var trimmed = sentence.Trim();
                if (trimmed.Length > 20)
                {
                    result.KeyPoints.Add(trimmed);
                }
            }

            return result;
        }

        private DocumentIntentResult ParseIntentResponse(string response)
        {
            try
            {
                // Extract JSON from the text response
                var jsonString = ExtractJsonFromResponse(response);
                if (string.IsNullOrEmpty(jsonString))
                {
                    // Fallback: try to parse the response directly
                    jsonString = response;
                }

                var json = JsonDocument.Parse(jsonString);
                var root = json.RootElement;

                var result = new DocumentIntentResult
                {
                    PrimaryIntent = root.TryGetProperty("primaryIntent", out var intent)
                        ? intent.GetString() ?? "Unknown" : "Unknown",
                    Confidence = root.TryGetProperty("confidence", out var conf)
                        ? conf.GetDouble() : 0.5,
                    SuggestedAction = root.TryGetProperty("suggestedAction", out var action)
                        ? action.GetString() ?? "" : ""
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse intent response");
                return new DocumentIntentResult
                {
                    PrimaryIntent = "Unknown",
                    Confidence = 0
                };
            }
        }

        private string ExtractJsonFromResponse(string response)
        {
            try
            {
                // First, try to find JSON blocks in the text
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    return response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }
                
                // If no JSON found, return empty string
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string FormatExtractedContent(DocumentContent extractedContent)
        {
            var formatted = new StringBuilder();
            
            // Add content type header
            formatted.AppendLine($"[Document Type: {extractedContent.ContentType}]");
            
            // Add metadata if available
            if (extractedContent.Metadata.Any())
            {
                formatted.AppendLine("\n[Metadata]");
                foreach (var kvp in extractedContent.Metadata.Take(5)) // Limit metadata items
                {
                    formatted.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }
            
            // Add main content
            formatted.AppendLine("\n[Content]");
            formatted.AppendLine(extractedContent.Text);
            
            // Add tables if extracted
            if (extractedContent.ExtractedTables.Any())
            {
                formatted.AppendLine("\n[Extracted Tables]");
                foreach (var table in extractedContent.ExtractedTables.Take(3)) // Limit tables
                {
                    formatted.AppendLine(table);
                }
            }
            
            // Note if content was truncated
            if (extractedContent.IsTruncated)
            {
                formatted.AppendLine("\n[Note: Content was truncated due to length limits]");
            }
            
            return formatted.ToString();
        }

        public void Dispose()
        {
            _bedrockClient?.Dispose();
        }
    }
}