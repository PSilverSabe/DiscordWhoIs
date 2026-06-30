using System.Security.Cryptography;

namespace DiscordWhoIs.Core.Configuration.Models;

public class UploadConfiguration : FileLocationConfiguration
{
    public bool IncludeExceptionDetails { get; set; } = false;

    /// <summary>
    /// The API key hash (use PBKDF2 or similar).
    /// Generate with: Convert.ToBase64String(KeyDerivation.Pbkdf2(apiKey, salt, KeyDerivationPrf.HMACSHA256, 10000, 32))
    /// </summary>
    public required string ApiKeyHash { get; set; }

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

            byte[] hashBytes = Convert.FromBase64String(ApiKeyHash);

            // PBKDF2 with HMACSHA256 and 10000 iterations produces 32 bytes
            if (hashBytes.Length != 32)
            {
                return false; // Invalid hash format
            }

            // Hash the provided key with the same algorithm
            byte[] providedBytes = System.Text.Encoding.UTF8.GetBytes(providedKey);
            return CryptographicOperations.FixedTimeEquals(providedBytes, hashBytes);
        }
        catch
        {
            return false;
        }
    }
}
