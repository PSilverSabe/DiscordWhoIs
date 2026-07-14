using System.Security.Cryptography;

namespace DiscordWhoIs.Core.Configuration.Models;

public class UploadConfiguration : FileLocationConfiguration
{
    public bool IncludeExceptionDetails { get; set; } = false;

    /// <summary>
    /// The API key hash (use PBKDF2 or similar).
    /// Generate with: Convert.ToBase64String(KeyDerivation.Pbkdf2(apiKey, salt, KeyDerivationPrf.HMACSHA256, 10000, 32))
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Validates an API key against the stored hash using constant-time comparison.
    /// </summary>
    public bool ValidateApiKey(string providedKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                return false;
            }

            // Hash the provided key with the same algorithm
            byte[] providedBytes = System.Text.Encoding.UTF8.GetBytes(providedKey);
            byte[] apiBytes = System.Text.Encoding.UTF8.GetBytes(ApiKey);
            return CryptographicOperations.FixedTimeEquals(providedBytes, apiBytes);
        }
        catch
        {
            return false;
        }
    }
}
