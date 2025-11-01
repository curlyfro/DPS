using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace DocumentProcessor.Infrastructure.Services
{
    /// <summary>
    /// Service for retrieving secrets from AWS Secrets Manager
    /// </summary>
    public class SecretsManagerService
    {
        private readonly IAmazonSecretsManager _secretsManager;

        public SecretsManagerService()
        {
            _secretsManager = new AmazonSecretsManagerClient(RegionEndpoint.USEast1);
        }

        public SecretsManagerService(IAmazonSecretsManager secretsManager)
        {
            _secretsManager = secretsManager;
        }

        /// <summary>
        /// Retrieves a secret by its exact name
        /// </summary>
        /// <param name="secretName">The name of the secret</param>
        /// <returns>The secret value as a string</returns>
        public async Task<string> GetSecretAsync(string secretName)
        {
            try
            {
                var request = new GetSecretValueRequest
                {
                    SecretId = secretName
                };

                var response = await _secretsManager.GetSecretValueAsync(request);
                return response.SecretString;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving secret '{secretName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finds a secret by searching for a description that starts with a given prefix
        /// </summary>
        /// <param name="descriptionPrefix">The prefix to search for in the description</param>
        /// <returns>The secret value as a string</returns>
        public async Task<string> GetSecretByDescriptionPrefixAsync(string descriptionPrefix)
        {
            try
            {
                var listRequest = new ListSecretsRequest();
                var listResponse = await _secretsManager.ListSecretsAsync(listRequest);

                foreach (var secret in listResponse.SecretList)
                {
                    if (!string.IsNullOrEmpty(secret.Description) &&
                        secret.Description.StartsWith(descriptionPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found the secret, now retrieve its value
                        var getRequest = new GetSecretValueRequest
                        {
                            SecretId = secret.ARN
                        };
                        var getResponse = await _secretsManager.GetSecretValueAsync(getRequest);
                        return getResponse.SecretString;
                    }
                }

                throw new Exception($"No secret found with description starting with '{descriptionPrefix}'");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error finding secret by description prefix '{descriptionPrefix}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a JSON secret string and extracts a specific field
        /// </summary>
        /// <param name="secretJson">The JSON secret string</param>
        /// <param name="fieldName">The field name to extract</param>
        /// <returns>The field value</returns>
        public string GetFieldFromSecret(string secretJson, string fieldName)
        {
            try
            {
                using var document = JsonDocument.Parse(secretJson);
                if (document.RootElement.TryGetProperty(fieldName, out var value))
                {
                    // Handle both string and number types
                    return value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString() ?? string.Empty,
                        JsonValueKind.Number => value.GetInt32().ToString(),
                        _ => value.ToString()
                    };
                }
                throw new Exception($"Field '{fieldName}' not found in secret");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing secret field '{fieldName}': {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Data model for database credentials from Secrets Manager
    /// </summary>
    public class DatabaseCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data model for database connection information from Secrets Manager
    /// </summary>
    public class DatabaseConnectionInfo
    {
        public string Host { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string DbName { get; set; } = string.Empty;
    }
}
