using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        public string ModelId => _options.ClassificationModelId;
        public string ProviderName => "Amazon Bedrock";

        public BedrockAIProcessor(
            ILogger<BedrockAIProcessor> logger,
            IOptions<BedrockOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            
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

                // Read content from stream
                string content = await ReadStreamContent(documentContent);
                
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

                string content = await ReadStreamContent(documentContent);
                
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

                string content = await ReadStreamContent(documentContent);
                
                var prompt = BuildSummarizationPrompt(content, 500);
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

                string content = await ReadStreamContent(documentContent);
                
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
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        private async Task<string> InvokeModelAsync(
            string modelId, 
            string prompt, 
            CancellationToken cancellationToken)
        {
            // If using mock responses for testing
            if (_options.UseMockResponses)
            {
                _logger.LogDebug("Using mock response for Bedrock model {ModelId}", modelId);
                return GenerateMockResponse(modelId, prompt);
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

        private string GenerateMockResponse(string modelId, string prompt)
        {
            // Generate appropriate mock responses based on the prompt
            if (prompt.Contains("classify"))
            {
                return JsonSerializer.Serialize(new
                {
                    category = "Invoice",
                    confidence = 0.95,
                    tags = new[] { "financial", "billing", "payment" }
                });
            }
            else if (prompt.Contains("extract"))
            {
                return JsonSerializer.Serialize(new
                {
                    entities = new[]
                    {
                        new { type = "DATE", value = "2024-01-15", confidence = 0.9 },
                        new { type = "AMOUNT", value = "$1,234.56", confidence = 0.95 }
                    }
                });
            }
            else if (prompt.Contains("summar"))
            {
                return "This document contains important financial information including invoice details and payment terms.";
            }
            else if (prompt.Contains("intent"))
            {
                return JsonSerializer.Serialize(new
                {
                    primaryIntent = "Payment Request",
                    confidence = 0.85,
                    suggestedAction = "Process payment"
                });
            }
            
            return "Mock response";
        }

        private string BuildClassificationPrompt(string content, string fileName)
        {
            return $@"Analyze the following document and classify it.

Document name: {fileName}
Document content:
{content.Substring(0, Math.Min(content.Length, 3000))}

Respond with a JSON object containing:
- category: the document category (e.g., Invoice, Contract, Report, Letter, etc.)
- confidence: a number between 0 and 1
- tags: array of relevant tags

Example response:
{{""category"": ""Invoice"", ""confidence"": 0.95, ""tags"": [""financial"", ""billing""]}}";
        }

        private string BuildExtractionPrompt(string content, string documentType)
        {
            return $@"Extract key entities from this {documentType} document.

Document content:
{content.Substring(0, Math.Min(content.Length, 3000))}

Respond with a JSON object containing an 'entities' array, where each entity has:
- type: entity type (e.g., DATE, PERSON, ORGANIZATION, AMOUNT, etc.)
- value: extracted value
- confidence: extraction confidence (0-1)

Focus on extracting relevant information for a {documentType}.";
        }

        private string BuildSummarizationPrompt(string content, int maxLength)
        {
            return $@"Provide a concise summary of the following document in approximately {maxLength} characters.

Document content:
{content}

Create a clear, informative summary that captures the main points and purpose of the document.";
        }

        private string BuildIntentPrompt(string content)
        {
            return $@"Analyze the intent or purpose of this document.

Document content:
{content.Substring(0, Math.Min(content.Length, 3000))}

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
                var json = JsonDocument.Parse(response);
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
                var json = JsonDocument.Parse(response);
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
                var json = JsonDocument.Parse(response);
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

        public void Dispose()
        {
            _bedrockClient?.Dispose();
        }
    }
}