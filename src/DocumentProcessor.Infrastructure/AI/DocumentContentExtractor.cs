using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using System.Globalization;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Infrastructure.AI
{
    /// <summary>
    /// Extracts content from various document types for AI processing
    /// </summary>
    public class DocumentContentExtractor
    {
        private readonly ILogger<DocumentContentExtractor> _logger;
        private const int MaxContentLength = 50000; // Character limit for AI processing

        public DocumentContentExtractor(ILogger<DocumentContentExtractor> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Extracts text content from document based on file type
        /// </summary>
        public async Task<DocumentContent> ExtractContentAsync(Document document, Stream documentStream)
        {
            var extension = Path.GetExtension(document.FileName)?.ToLower() ?? "";
            _logger.LogInformation("Extracting content from {Extension} file: {FileName}", 
                extension, document.FileName);

            try
            {
                var content = extension switch
                {
                    ".pdf" => await ExtractPdfContentAsync(documentStream),
                    ".txt" or ".log" or ".md" => await ExtractTextContentAsync(documentStream),
                    ".csv" => await ExtractCsvContentAsync(documentStream),
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" => 
                        await ExtractImageContentAsync(documentStream, document.FileName),
                    ".mp3" or ".wav" or ".m4a" or ".aac" => 
                        await ExtractAudioContentAsync(documentStream, document.FileName),
                    ".docx" => await ExtractDocxContentAsync(documentStream),
                    ".xlsx" => await ExtractExcelContentAsync(documentStream),
                    ".json" => await ExtractJsonContentAsync(documentStream),
                    ".xml" => await ExtractXmlContentAsync(documentStream),
                    ".html" or ".htm" => await ExtractHtmlContentAsync(documentStream),
                    _ => await ExtractGenericTextContentAsync(documentStream, document.FileName)
                };

                // Truncate if necessary
                if (content.Text.Length > MaxContentLength)
                {
                    _logger.LogWarning("Content truncated from {Original} to {Max} characters", 
                        content.Text.Length, MaxContentLength);
                    content.Text = content.Text.Substring(0, MaxContentLength) + 
                        "\n\n[Content truncated due to length...]";
                    content.IsTruncated = true;
                }

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting content from {FileName}", document.FileName);
                return new DocumentContent
                {
                    Text = $"[Error extracting content from {extension} file: {ex.Message}]",
                    ContentType = "error",
                    Metadata = new Dictionary<string, string>
                    {
                        ["error"] = ex.Message,
                        ["fileType"] = extension
                    }
                };
            }
        }

        /// <summary>
        /// Extracts text from PDF documents with advanced features
        /// </summary>
        private async Task<DocumentContent> ExtractPdfContentAsync(Stream pdfStream)
        {
            var content = new DocumentContent
            {
                ContentType = "pdf",
                Metadata = new Dictionary<string, string>()
            };

            var textBuilder = new StringBuilder();
            var tables = new List<string>();
            int pageCount = 0;

            await Task.Run(() =>
            {
                using var document = PdfDocument.Open(pdfStream);
                pageCount = document.NumberOfPages;
                content.Metadata["pageCount"] = pageCount.ToString();

                foreach (var page in document.GetPages())
                {
                    // Extract text
                    var pageText = ContentOrderTextExtractor.GetText(page);
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine($"--- Page {page.Number} ---");
                        textBuilder.AppendLine(pageText);
                    }

                    // Extract tables if present
                    var words = page.GetWords();
                    if (words.Any())
                    {
                        // Simple table detection based on aligned text
                        var lines = words.GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                                        .OrderBy(g => g.Key);
                        
                        // Check for table-like structures
                        foreach (var line in lines)
                        {
                            var orderedWords = line.OrderBy(w => w.BoundingBox.Left).ToList();
                            if (orderedWords.Count > 2) // Potential table row
                            {
                                var row = string.Join(" | ", orderedWords.Select(w => w.Text));
                                if (row.Contains("|"))
                                {
                                    tables.Add(row);
                                }
                            }
                        }
                    }

                    // Check content length to avoid exceeding limits
                    if (textBuilder.Length > MaxContentLength)
                    {
                        textBuilder.AppendLine("\n[Remaining pages truncated]");
                        break;
                    }
                }
            });

            content.Text = textBuilder.ToString();
            
            if (tables.Any())
            {
                content.Metadata["tables"] = string.Join("\n", tables.Take(10)); // Store first 10 table rows
                content.Metadata["tableCount"] = tables.Count.ToString();
            }

            _logger.LogInformation("Extracted {Characters} characters from {Pages} page PDF", 
                content.Text.Length, pageCount);

            return content;
        }

        /// <summary>
        /// Extracts content from CSV files with structure preservation
        /// </summary>
        private async Task<DocumentContent> ExtractCsvContentAsync(Stream csvStream)
        {
            var content = new DocumentContent
            {
                ContentType = "csv",
                Metadata = new Dictionary<string, string>()
            };

            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                BadDataFound = null,
                MissingFieldFound = null
            });

            var records = new List<dynamic>();
            var headers = new List<string>();
            var textBuilder = new StringBuilder();

            await Task.Run(() =>
            {
                // Read headers
                csv.Read();
                csv.ReadHeader();
                headers = csv.HeaderRecord?.ToList() ?? new List<string>();
                
                if (headers.Any())
                {
                    textBuilder.AppendLine("CSV Structure:");
                    textBuilder.AppendLine($"Columns: {string.Join(", ", headers)}");
                    textBuilder.AppendLine("\nData Sample:");
                    textBuilder.AppendLine(string.Join(" | ", headers));
                    textBuilder.AppendLine(new string('-', headers.Count * 10));
                }

                // Read records
                int rowCount = 0;
                while (csv.Read() && rowCount < 100) // Limit to first 100 rows for processing
                {
                    var row = new List<string>();
                    for (int i = 0; i < headers.Count; i++)
                    {
                        try
                        {
                            row.Add(csv.GetField(i) ?? "");
                        }
                        catch
                        {
                            row.Add("");
                        }
                    }
                    textBuilder.AppendLine(string.Join(" | ", row));
                    rowCount++;
                }

                // Count total rows
                while (csv.Read())
                {
                    rowCount++;
                }

                content.Metadata["rowCount"] = rowCount.ToString();
                content.Metadata["columnCount"] = headers.Count.ToString();
                content.Metadata["columns"] = string.Join(",", headers);
            });

            content.Text = textBuilder.ToString();
            
            // Add statistical summary
            if (headers.Any())
            {
                content.Text += $"\n\nTotal Rows: {content.Metadata["rowCount"]}";
                content.Text += $"\nTotal Columns: {content.Metadata["columnCount"]}";
            }

            _logger.LogInformation("Extracted CSV with {Rows} rows and {Columns} columns", 
                content.Metadata["rowCount"], content.Metadata["columnCount"]);

            return content;
        }

        /// <summary>
        /// Processes image files with OCR capability
        /// </summary>
        private async Task<DocumentContent> ExtractImageContentAsync(Stream imageStream, string fileName)
        {
            var content = new DocumentContent
            {
                ContentType = "image",
                Metadata = new Dictionary<string, string>()
            };

            await Task.Run(() =>
            {
                using var image = Image.Load<Rgba32>(imageStream);
                
                // Store image metadata
                content.Metadata["width"] = image.Width.ToString();
                content.Metadata["height"] = image.Height.ToString();
                content.Metadata["format"] = image.Metadata.DecodedImageFormat?.Name ?? "unknown";
                
                // Generate image description
                var description = new StringBuilder();
                description.AppendLine($"Image File: {fileName}");
                description.AppendLine($"Dimensions: {image.Width}x{image.Height} pixels");
                description.AppendLine($"Format: {content.Metadata["format"]}");
                
                // Analyze image characteristics
                var histogram = AnalyzeImageHistogram(image);
                description.AppendLine($"Dominant Colors: {histogram}");
                
                // Check if image might contain text (high contrast areas)
                bool likelyContainsText = DetectPossibleText(image);
                if (likelyContainsText)
                {
                    description.AppendLine("\nNote: This image may contain text. OCR processing recommended.");
                    content.Metadata["possibleText"] = "true";
                    
                    // In production, integrate with Tesseract OCR here
                    // For now, we'll add a placeholder
                    description.AppendLine("[OCR text extraction would be performed here]");
                }
                
                content.Text = description.ToString();
            });

            _logger.LogInformation("Processed image {FileName} ({Width}x{Height})", 
                fileName, content.Metadata["width"], content.Metadata["height"]);

            return content;
        }

        /// <summary>
        /// Processes audio files with transcription metadata
        /// </summary>
        private async Task<DocumentContent> ExtractAudioContentAsync(Stream audioStream, string fileName)
        {
            var content = new DocumentContent
            {
                ContentType = "audio",
                Metadata = new Dictionary<string, string>()
            };

            var extension = Path.GetExtension(fileName)?.ToLower() ?? "";
            var description = new StringBuilder();
            description.AppendLine($"Audio File: {fileName}");

            try
            {
                await Task.Run(() =>
                {
                    // For MP3 files
                    if (extension == ".mp3")
                    {
                        using var mp3 = new Mp3FileReader(audioStream);
                        content.Metadata["duration"] = mp3.TotalTime.ToString(@"hh\:mm\:ss");
                        content.Metadata["bitrate"] = mp3.Mp3WaveFormat.AverageBytesPerSecond.ToString();
                        content.Metadata["sampleRate"] = mp3.Mp3WaveFormat.SampleRate.ToString();
                        content.Metadata["channels"] = mp3.Mp3WaveFormat.Channels.ToString();
                        
                        description.AppendLine($"Duration: {mp3.TotalTime:hh\\:mm\\:ss}");
                        description.AppendLine($"Bitrate: {mp3.Mp3WaveFormat.AverageBytesPerSecond / 1000} kbps");
                        description.AppendLine($"Sample Rate: {mp3.Mp3WaveFormat.SampleRate} Hz");
                        description.AppendLine($"Channels: {(mp3.Mp3WaveFormat.Channels == 2 ? "Stereo" : "Mono")}");
                    }
                    // For WAV files
                    else if (extension == ".wav")
                    {
                        using var wav = new WaveFileReader(audioStream);
                        content.Metadata["duration"] = wav.TotalTime.ToString(@"hh\:mm\:ss");
                        content.Metadata["sampleRate"] = wav.WaveFormat.SampleRate.ToString();
                        content.Metadata["channels"] = wav.WaveFormat.Channels.ToString();
                        content.Metadata["bitsPerSample"] = wav.WaveFormat.BitsPerSample.ToString();
                        
                        description.AppendLine($"Duration: {wav.TotalTime:hh\\:mm\\:ss}");
                        description.AppendLine($"Sample Rate: {wav.WaveFormat.SampleRate} Hz");
                        description.AppendLine($"Channels: {(wav.WaveFormat.Channels == 2 ? "Stereo" : "Mono")}");
                        description.AppendLine($"Bits per Sample: {wav.WaveFormat.BitsPerSample}");
                    }
                });

                description.AppendLine("\n[Audio Transcription]");
                description.AppendLine("Note: Audio transcription requires integration with speech-to-text service.");
                description.AppendLine("Recommended services: Amazon Transcribe, Azure Speech Services, or Google Speech-to-Text");
                
                // In production, integrate with Amazon Transcribe or similar service
                content.Metadata["transcriptionAvailable"] = "false";
                content.Metadata["transcriptionService"] = "not_configured";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract detailed audio metadata from {FileName}", fileName);
                description.AppendLine($"Audio format: {extension}");
                description.AppendLine("Detailed metadata extraction failed");
            }

            content.Text = description.ToString();
            
            _logger.LogInformation("Processed audio file {FileName}", fileName);
            return content;
        }

        /// <summary>
        /// Extracts text from plain text files with encoding detection
        /// </summary>
        private async Task<DocumentContent> ExtractTextContentAsync(Stream textStream)
        {
            var content = new DocumentContent
            {
                ContentType = "text",
                Metadata = new Dictionary<string, string>()
            };

            // Try to detect encoding
            textStream.Position = 0;
            var buffer = new byte[4];
            await textStream.ReadAsync(buffer, 0, 4);
            textStream.Position = 0;

            Encoding encoding = DetectEncoding(buffer);
            content.Metadata["encoding"] = encoding.EncodingName;

            using var reader = new StreamReader(textStream, encoding);
            content.Text = await reader.ReadToEndAsync();
            
            // Add line and word count
            var lines = content.Text.Split('\n');
            var words = content.Text.Split(new[] { ' ', '\n', '\r', '\t' }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            content.Metadata["lineCount"] = lines.Length.ToString();
            content.Metadata["wordCount"] = words.Length.ToString();
            content.Metadata["characterCount"] = content.Text.Length.ToString();

            _logger.LogInformation("Extracted text file with {Lines} lines, {Words} words", 
                lines.Length, words.Length);

            return content;
        }

        /// <summary>
        /// Extracts content from DOCX files (simplified)
        /// </summary>
        private async Task<DocumentContent> ExtractDocxContentAsync(Stream docxStream)
        {
            var content = new DocumentContent
            {
                ContentType = "docx",
                Metadata = new Dictionary<string, string>(),
                Text = "[DOCX Document]\n"
            };

            // For full DOCX support, use DocumentFormat.OpenXml package
            // This is a simplified placeholder
            content.Text += "Note: Full DOCX extraction requires DocumentFormat.OpenXml package.\n";
            content.Text += "The document would be processed to extract:\n";
            content.Text += "- Paragraphs and formatted text\n";
            content.Text += "- Tables and lists\n";
            content.Text += "- Headers and footers\n";
            content.Text += "- Document properties and metadata\n";
            
            content.Metadata["format"] = "Microsoft Word Document";
            
            return await Task.FromResult(content);
        }

        /// <summary>
        /// Extracts content from Excel files (simplified)
        /// </summary>
        private async Task<DocumentContent> ExtractExcelContentAsync(Stream excelStream)
        {
            var content = new DocumentContent
            {
                ContentType = "xlsx",
                Metadata = new Dictionary<string, string>(),
                Text = "[Excel Spreadsheet]\n"
            };

            // For full Excel support, use EPPlus or ClosedXML package
            // This is a simplified placeholder
            content.Text += "Note: Full Excel extraction requires EPPlus or ClosedXML package.\n";
            content.Text += "The spreadsheet would be processed to extract:\n";
            content.Text += "- Worksheet data and formulas\n";
            content.Text += "- Cell formatting and styles\n";
            content.Text += "- Charts and pivot tables metadata\n";
            content.Text += "- Named ranges and data validation\n";
            
            content.Metadata["format"] = "Microsoft Excel Spreadsheet";
            
            return await Task.FromResult(content);
        }

        /// <summary>
        /// Extracts and formats JSON content
        /// </summary>
        private async Task<DocumentContent> ExtractJsonContentAsync(Stream jsonStream)
        {
            var content = new DocumentContent
            {
                ContentType = "json",
                Metadata = new Dictionary<string, string>()
            };

            using var reader = new StreamReader(jsonStream);
            var jsonText = await reader.ReadToEndAsync();
            
            try
            {
                // Parse and reformat JSON for better readability
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonText);
                var formatted = System.Text.Json.JsonSerializer.Serialize(jsonDoc, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                
                content.Text = "JSON Document Structure:\n" + formatted;
                
                // Extract metadata about JSON structure
                var root = jsonDoc.RootElement;
                content.Metadata["type"] = root.ValueKind.ToString();
                
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    content.Metadata["propertyCount"] = root.EnumerateObject().Count().ToString();
                }
                else if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    content.Metadata["arrayLength"] = root.GetArrayLength().ToString();
                }
            }
            catch
            {
                content.Text = jsonText; // Return raw JSON if parsing fails
            }

            return content;
        }

        /// <summary>
        /// Extracts and formats XML content
        /// </summary>
        private async Task<DocumentContent> ExtractXmlContentAsync(Stream xmlStream)
        {
            var content = new DocumentContent
            {
                ContentType = "xml",
                Metadata = new Dictionary<string, string>()
            };

            using var reader = new StreamReader(xmlStream);
            var xmlText = await reader.ReadToEndAsync();
            
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xmlText);
                content.Text = "XML Document Structure:\n" + doc.ToString();
                
                // Extract metadata
                content.Metadata["rootElement"] = doc.Root?.Name.LocalName ?? "unknown";
                content.Metadata["elementCount"] = doc.Descendants().Count().ToString();
                content.Metadata["attributeCount"] = doc.Descendants()
                    .SelectMany(e => e.Attributes()).Count().ToString();
            }
            catch
            {
                content.Text = xmlText; // Return raw XML if parsing fails
            }

            return content;
        }

        /// <summary>
        /// Extracts text from HTML files
        /// </summary>
        private async Task<DocumentContent> ExtractHtmlContentAsync(Stream htmlStream)
        {
            var content = new DocumentContent
            {
                ContentType = "html",
                Metadata = new Dictionary<string, string>()
            };

            using var reader = new StreamReader(htmlStream);
            var htmlText = await reader.ReadToEndAsync();
            
            // Simple HTML tag removal (for production, use HtmlAgilityPack)
            var textOnly = System.Text.RegularExpressions.Regex.Replace(htmlText, 
                @"<[^>]+>", " ");
            textOnly = System.Text.RegularExpressions.Regex.Replace(textOnly, 
                @"\s+", " ");
            textOnly = System.Net.WebUtility.HtmlDecode(textOnly);
            
            content.Text = textOnly.Trim();
            
            // Extract title if present
            var titleMatch = System.Text.RegularExpressions.Regex.Match(htmlText, 
                @"<title>([^<]+)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                content.Metadata["title"] = titleMatch.Groups[1].Value;
            }

            return content;
        }

        /// <summary>
        /// Generic text extraction fallback
        /// </summary>
        private async Task<DocumentContent> ExtractGenericTextContentAsync(Stream stream, string fileName)
        {
            var content = new DocumentContent
            {
                ContentType = "unknown",
                Metadata = new Dictionary<string, string>
                {
                    ["fileName"] = fileName,
                    ["extension"] = Path.GetExtension(fileName)?.ToLower() ?? "none"
                }
            };

            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                content.Text = await reader.ReadToEndAsync();
                
                // If text appears to be binary, provide metadata instead
                if (IsBinaryContent(content.Text))
                {
                    stream.Position = 0;
                    var bytes = new byte[Math.Min(stream.Length, 1024)];
                    await stream.ReadAsync(bytes, 0, bytes.Length);
                    
                    content.Text = $"[Binary file: {fileName}]\n";
                    content.Text += $"File size: {stream.Length} bytes\n";
                    content.Text += $"File type: {content.Metadata["extension"]}\n";
                    content.Text += $"First bytes (hex): {BitConverter.ToString(bytes.Take(32).ToArray())}\n";
                    content.ContentType = "binary";
                }
            }
            catch
            {
                content.Text = $"[Unable to extract content from: {fileName}]";
            }

            return content;
        }

        /// <summary>
        /// Detects text encoding from byte order marks
        /// </summary>
        private Encoding DetectEncoding(byte[] buffer)
        {
            if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                return Encoding.UTF8;
            if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
                return Encoding.Unicode;
            if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            if (buffer.Length >= 4 && buffer[0] == 0xFF && buffer[1] == 0xFE && 
                buffer[2] == 0x00 && buffer[3] == 0x00)
                return Encoding.UTF32;
            
            return Encoding.UTF8; // Default
        }

        /// <summary>
        /// Checks if content appears to be binary
        /// </summary>
        private bool IsBinaryContent(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Check for null characters or high proportion of non-printable characters
            var nonPrintable = text.Take(1000).Count(c => char.IsControl(c) && c != '\n' && 
                c != '\r' && c != '\t');
            return nonPrintable > 100 || text.Contains('\0');
        }

        /// <summary>
        /// Analyzes image histogram for dominant colors
        /// </summary>
        private string AnalyzeImageHistogram(Image<Rgba32> image)
        {
            var colorCounts = new Dictionary<string, int>();
            
            // Sample pixels (every 10th pixel for performance)
            for (int y = 0; y < image.Height; y += 10)
            {
                for (int x = 0; x < image.Width; x += 10)
                {
                    var pixel = image[x, y];
                    var colorCategory = CategorizeColor(pixel);
                    
                    if (!colorCounts.ContainsKey(colorCategory))
                        colorCounts[colorCategory] = 0;
                    colorCounts[colorCategory]++;
                }
            }

            var dominantColors = colorCounts.OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .Select(kvp => kvp.Key);
            
            return string.Join(", ", dominantColors);
        }

        /// <summary>
        /// Categorizes a pixel color into broad categories
        /// </summary>
        private string CategorizeColor(Rgba32 pixel)
        {
            var r = pixel.R;
            var g = pixel.G;
            var b = pixel.B;
            
            // Grayscale detection
            if (Math.Abs(r - g) < 20 && Math.Abs(g - b) < 20)
            {
                if (r < 64) return "Black";
                if (r < 192) return "Gray";
                return "White";
            }
            
            // Primary color detection
            if (r > g && r > b) return "Red";
            if (g > r && g > b) return "Green";
            if (b > r && b > g) return "Blue";
            
            // Secondary colors
            if (r > 128 && g > 128 && b < 128) return "Yellow";
            if (r > 128 && b > 128 && g < 128) return "Magenta";
            if (g > 128 && b > 128 && r < 128) return "Cyan";
            
            return "Mixed";
        }

        /// <summary>
        /// Detects if image likely contains text based on contrast patterns
        /// </summary>
        private bool DetectPossibleText(Image<Rgba32> image)
        {
            int highContrastEdges = 0;
            int sampledPixels = 0;
            
            // Sample the image for high contrast edges (indicative of text)
            for (int y = 1; y < image.Height - 1; y += 5)
            {
                for (int x = 1; x < image.Width - 1; x += 5)
                {
                    var current = image[x, y];
                    var right = image[x + 1, y];
                    var bottom = image[x, y + 1];
                    
                    var currentGray = (current.R + current.G + current.B) / 3;
                    var rightGray = (right.R + right.G + right.B) / 3;
                    var bottomGray = (bottom.R + bottom.G + bottom.B) / 3;
                    
                    if (Math.Abs(currentGray - rightGray) > 100 || 
                        Math.Abs(currentGray - bottomGray) > 100)
                    {
                        highContrastEdges++;
                    }
                    sampledPixels++;
                }
            }
            
            // If more than 10% of sampled pixels are high contrast edges, likely contains text
            return (double)highContrastEdges / sampledPixels > 0.1;
        }
    }

    /// <summary>
    /// Represents extracted document content
    /// </summary>
    public class DocumentContent
    {
        public string Text { get; set; } = "";
        public string ContentType { get; set; } = "";
        public Dictionary<string, string> Metadata { get; set; } = new();
        public bool IsTruncated { get; set; }
        public List<string> ExtractedTables { get; set; } = new();
        public List<string> ExtractedImages { get; set; } = new();
    }
}