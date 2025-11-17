using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using System.Globalization;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Infrastructure.AI;

/// <summary>
/// Extracts content from various document types for AI processing
/// </summary>
public class DocumentContentExtractor(ILogger<DocumentContentExtractor> logger)
{
    private const int MaxContentLength = 50000; // Character limit for AI processing

    /// <summary>
    /// Extracts text content from document based on file type
    /// </summary>
    public async Task<DocumentContent> ExtractContentAsync(Document document, Stream documentStream)
    {
        var extension = Path.GetExtension(document.FileName)?.ToLower() ?? "";
        logger.LogInformation("Extracting content from {Extension} file: {FileName}",
            extension, document.FileName);

        try
        {
            var content = extension switch
            {
                ".pdf" => await ExtractPdfContentAsync(documentStream),
                ".txt" or ".log" or ".md" => await ExtractTextContentAsync(documentStream),
                ".csv" => await ExtractCsvContentAsync(documentStream),
                _ => new DocumentContent
                {
                    Text = $"[Unsupported file type: {extension}. Only PDF, TXT, and CSV files are supported.]",
                    ContentType = "unsupported",
                    Metadata = new Dictionary<string, string> { ["fileType"] = extension }
                }
            };

            // Truncate if necessary
            if (content.Text.Length > MaxContentLength)
            {
                logger.LogWarning("Content truncated from {Original} to {Max} characters",
                    content.Text.Length, MaxContentLength);
                content.Text = content.Text.Substring(0, MaxContentLength) +
                               "\n\n[Content truncated due to length...]";
                content.IsTruncated = true;
            }

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting content from {FileName}", document.FileName);
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

        logger.LogInformation("Extracted {Characters} characters from {Pages} page PDF",
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

        logger.LogInformation("Extracted CSV with {Rows} rows and {Columns} columns",
            content.Metadata["rowCount"], content.Metadata["columnCount"]);

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

        logger.LogInformation("Extracted text file with {Lines} lines, {Words} words",
            lines.Length, words.Length);

        return content;
    }

    /// <summary>
    /// Detects text encoding from byte order marks
    /// </summary>
    private static Encoding DetectEncoding(byte[] buffer)
    {
        return buffer.Length switch
        {
            >= 3 when buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF => Encoding.UTF8,
            >= 2 when buffer[0] == 0xFF && buffer[1] == 0xFE => Encoding.Unicode,
            >= 2 when buffer[0] == 0xFE && buffer[1] == 0xFF => Encoding.BigEndianUnicode,
            >= 4 when buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00 =>
                Encoding.UTF32,
            _ => Encoding.UTF8
        };
    }
}