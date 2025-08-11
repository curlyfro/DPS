using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Providers
{
    /// <summary>
    /// Mock S3 provider for testing without AWS credentials
    /// Uses local file system to simulate S3 operations
    /// </summary>
    public class MockS3Provider : IDocumentSourceProvider
    {
        private readonly ILogger<MockS3Provider> _logger;
        private readonly string _mockBucketPath;
        private readonly string _bucketName;
        private readonly Dictionary<string, DocumentInfo> _mockMetadata;

        public string ProviderName => "MockS3";

        public MockS3Provider(ILogger<MockS3Provider> logger, IConfiguration configuration)
        {
            _logger = logger;
            _bucketName = configuration["AWS:S3:BucketName"] ?? "document-processor-bucket";
            _mockBucketPath = Path.Combine("mock-s3", _bucketName);

            // Ensure mock bucket directory exists
            if (!Directory.Exists(_mockBucketPath))
            {
                Directory.CreateDirectory(_mockBucketPath);
                _logger.LogInformation("Created mock S3 bucket at: {Path}", _mockBucketPath);
            }

            _mockMetadata = new Dictionary<string, DocumentInfo>();
        }

        public async Task<Stream> GetDocumentStreamAsync(string path)
        {
            try
            {
                var s3Key = NormalizeS3Key(path);
                var localPath = GetLocalPath(s3Key);

                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException($"Object not found in mock S3: s3://{_bucketName}/{s3Key}");
                }

                _logger.LogInformation("Getting object from mock S3: s3://{Bucket}/{Key}", _bucketName, s3Key);
                return await Task.FromResult(new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object from mock S3: {Path}", path);
                throw;
            }
        }

        public async Task<byte[]> GetDocumentBytesAsync(string path)
        {
            try
            {
                var s3Key = NormalizeS3Key(path);
                var localPath = GetLocalPath(s3Key);

                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException($"Object not found in mock S3: s3://{_bucketName}/{s3Key}");
                }

                _logger.LogInformation("Getting object bytes from mock S3: s3://{Bucket}/{Key}", _bucketName, s3Key);
                return await File.ReadAllBytesAsync(localPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object bytes from mock S3: {Path}", path);
                throw;
            }
        }

        public async Task<string> SaveDocumentAsync(Stream documentStream, string fileName)
        {
            try
            {
                // Generate S3-like key structure
                var s3Key = GenerateS3Key(fileName);
                var localPath = GetLocalPath(s3Key);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save file
                using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await documentStream.CopyToAsync(fileStream);
                }

                // Store metadata
                _mockMetadata[s3Key] = new DocumentInfo
                {
                    FileName = fileName,
                    Path = s3Key,
                    Size = new FileInfo(localPath).Length,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    ContentType = GetContentType(Path.GetExtension(fileName))
                };

                _logger.LogInformation("Object uploaded to mock S3: s3://{Bucket}/{Key}", _bucketName, s3Key);
                return s3Key;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading object to mock S3: {FileName}", fileName);
                throw;
            }
        }

        public async Task<string> SaveDocumentAsync(byte[] documentBytes, string fileName)
        {
            using var stream = new MemoryStream(documentBytes);
            return await SaveDocumentAsync(stream, fileName);
        }

        public async Task<bool> DeleteDocumentAsync(string path)
        {
            try
            {
                var s3Key = NormalizeS3Key(path);
                var localPath = GetLocalPath(s3Key);

                if (File.Exists(localPath))
                {
                    await Task.Run(() => File.Delete(localPath));
                    _mockMetadata.Remove(s3Key);
                    _logger.LogInformation("Object deleted from mock S3: s3://{Bucket}/{Key}", _bucketName, s3Key);
                    return true;
                }

                _logger.LogWarning("Object not found in mock S3: s3://{Bucket}/{Key}", _bucketName, s3Key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting object from mock S3: {Path}", path);
                throw;
            }
        }

        public async Task<bool> DocumentExistsAsync(string path)
        {
            try
            {
                var s3Key = NormalizeS3Key(path);
                var localPath = GetLocalPath(s3Key);
                var exists = File.Exists(localPath);
                
                _logger.LogDebug("Checking object existence in mock S3: s3://{Bucket}/{Key} - {Exists}", 
                    _bucketName, s3Key, exists);
                
                return await Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking object existence in mock S3: {Path}", path);
                return false;
            }
        }

        public async Task<DocumentInfo> GetDocumentInfoAsync(string path)
        {
            try
            {
                var s3Key = NormalizeS3Key(path);
                
                // Check if we have cached metadata
                if (_mockMetadata.TryGetValue(s3Key, out var cachedInfo))
                {
                    return cachedInfo;
                }

                var localPath = GetLocalPath(s3Key);
                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException($"Object not found in mock S3: s3://{_bucketName}/{s3Key}");
                }

                var fileInfo = new FileInfo(localPath);
                var documentInfo = new DocumentInfo
                {
                    FileName = Path.GetFileName(s3Key),
                    Path = s3Key,
                    Size = fileInfo.Length,
                    CreatedAt = fileInfo.CreationTimeUtc,
                    ModifiedAt = fileInfo.LastWriteTimeUtc,
                    ContentType = GetContentType(fileInfo.Extension)
                };

                // Add S3-like metadata
                documentInfo.Metadata["Bucket"] = _bucketName;
                documentInfo.Metadata["Key"] = s3Key;
                documentInfo.Metadata["ETag"] = GenerateETag(fileInfo);
                documentInfo.Metadata["StorageClass"] = "STANDARD";

                return await Task.FromResult(documentInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object info from mock S3: {Path}", path);
                throw;
            }
        }

        public async Task<IEnumerable<DocumentInfo>> ListDocumentsAsync(string path)
        {
            try
            {
                var prefix = NormalizeS3Key(path);
                var localPath = GetLocalPath(prefix);
                var documents = new List<DocumentInfo>();

                if (!Directory.Exists(localPath))
                {
                    return documents;
                }

                var files = Directory.GetFiles(localPath, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var s3Key = GetS3Key(file);

                    documents.Add(new DocumentInfo
                    {
                        FileName = fileInfo.Name,
                        Path = s3Key,
                        Size = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTimeUtc,
                        ModifiedAt = fileInfo.LastWriteTimeUtc,
                        ContentType = GetContentType(fileInfo.Extension),
                        Metadata = new Dictionary<string, string>
                        {
                            ["Bucket"] = _bucketName,
                            ["Key"] = s3Key
                        }
                    });
                }

                _logger.LogInformation("Listed {Count} objects from mock S3 with prefix: {Prefix}", 
                    documents.Count, prefix);
                
                return await Task.FromResult(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing objects in mock S3: {Path}", path);
                throw;
            }
        }

        public async Task<string> GetDownloadUrlAsync(string path, TimeSpan expiration)
        {
            try
            {
                var s3Key = NormalizeS3Key(path);
                
                if (!await DocumentExistsAsync(s3Key))
                {
                    throw new FileNotFoundException($"Object not found in mock S3: s3://{_bucketName}/{s3Key}");
                }

                // Generate a mock pre-signed URL
                var expirationTime = DateTime.UtcNow.Add(expiration);
                var signature = GenerateMockSignature(s3Key, expirationTime);
                
                var url = $"https://{_bucketName}.s3.amazonaws.com/{s3Key}" +
                         $"?X-Amz-Expires={expiration.TotalSeconds}" +
                         $"&X-Amz-Signature={signature}" +
                         $"&X-Amz-Algorithm=AWS4-HMAC-SHA256";

                _logger.LogInformation("Generated mock pre-signed URL for: s3://{Bucket}/{Key}", _bucketName, s3Key);
                return await Task.FromResult(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating pre-signed URL for mock S3: {Path}", path);
                throw;
            }
        }

        public async Task<bool> MoveDocumentAsync(string sourcePath, string destinationPath)
        {
            try
            {
                var sourceKey = NormalizeS3Key(sourcePath);
                var destKey = NormalizeS3Key(destinationPath);
                
                var sourceLocal = GetLocalPath(sourceKey);
                var destLocal = GetLocalPath(destKey);

                if (!File.Exists(sourceLocal))
                {
                    _logger.LogWarning("Source object not found in mock S3: s3://{Bucket}/{Key}", _bucketName, sourceKey);
                    return false;
                }

                // Ensure destination directory exists
                var destDirectory = Path.GetDirectoryName(destLocal);
                if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                }

                await Task.Run(() => File.Move(sourceLocal, destLocal, true));

                // Update metadata
                if (_mockMetadata.TryGetValue(sourceKey, out var metadata))
                {
                    _mockMetadata.Remove(sourceKey);
                    metadata.Path = destKey;
                    _mockMetadata[destKey] = metadata;
                }

                _logger.LogInformation("Object moved in mock S3 from s3://{Bucket}/{Source} to s3://{Bucket}/{Dest}", 
                    _bucketName, sourceKey, destKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving object in mock S3 from {Source} to {Dest}", sourcePath, destinationPath);
                throw;
            }
        }

        public async Task<bool> CopyDocumentAsync(string sourcePath, string destinationPath)
        {
            try
            {
                var sourceKey = NormalizeS3Key(sourcePath);
                var destKey = NormalizeS3Key(destinationPath);
                
                var sourceLocal = GetLocalPath(sourceKey);
                var destLocal = GetLocalPath(destKey);

                if (!File.Exists(sourceLocal))
                {
                    _logger.LogWarning("Source object not found in mock S3: s3://{Bucket}/{Key}", _bucketName, sourceKey);
                    return false;
                }

                // Ensure destination directory exists
                var destDirectory = Path.GetDirectoryName(destLocal);
                if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                }

                await Task.Run(() => File.Copy(sourceLocal, destLocal, true));

                // Copy metadata
                if (_mockMetadata.TryGetValue(sourceKey, out var metadata))
                {
                    _mockMetadata[destKey] = new DocumentInfo
                    {
                        FileName = metadata.FileName,
                        Path = destKey,
                        Size = metadata.Size,
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow,
                        ContentType = metadata.ContentType,
                        Metadata = new Dictionary<string, string>(metadata.Metadata)
                    };
                }

                _logger.LogInformation("Object copied in mock S3 from s3://{Bucket}/{Source} to s3://{Bucket}/{Dest}", 
                    _bucketName, sourceKey, destKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying object in mock S3 from {Source} to {Dest}", sourcePath, destinationPath);
                throw;
            }
        }

        private string NormalizeS3Key(string path)
        {
            // Remove leading/trailing slashes and normalize path separators
            return path.Trim('/', '\\').Replace('\\', '/');
        }

        private string GetLocalPath(string s3Key)
        {
            return Path.Combine(_mockBucketPath, s3Key.Replace('/', Path.DirectorySeparatorChar));
        }

        private string GetS3Key(string localPath)
        {
            var relativePath = Path.GetRelativePath(_mockBucketPath, localPath);
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        private string GenerateS3Key(string fileName)
        {
            var date = DateTime.UtcNow;
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
            var cleanFileName = Path.GetFileName(fileName);
            
            return $"documents/{date:yyyy/MM/dd}/{guid}/{cleanFileName}";
        }

        private string GenerateETag(FileInfo fileInfo)
        {
            // Simple mock ETag generation
            var hash = $"{fileInfo.Length}-{fileInfo.LastWriteTimeUtc.Ticks}".GetHashCode();
            return $"\"{hash:X}\"";
        }

        private string GenerateMockSignature(string key, DateTime expiration)
        {
            // Simple mock signature generation
            var data = $"{key}{expiration:O}{_bucketName}";
            var hash = data.GetHashCode();
            return Convert.ToBase64String(BitConverter.GetBytes(hash));
        }

        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".json" => "application/json",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }
    }
}