namespace DiscordWhoIs.HumanFakers
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;

    public static class UserAgentProvider
    {
        /// <summary>
        /// Enables or disables Advanced Mode globally.
        /// Override this in appsettings or env vars if you want.
        /// </summary>
        public static bool AdvancedModeEnabled { get; set; } = true;

        private static readonly RandomNumberGenerator _secureRng = RandomNumberGenerator.Create();

        private static readonly List<string> _userAgents = new()
        {
            // Firefox
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:98.0) Gecko/20100101 Firefox/98.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:102.0) Gecko/20100101 Firefox/102.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:115.0) Gecko/20100101 Firefox/115.0",

            // Chrome (Windows)
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",

            // Chrome (Linux)
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        };

        /// <summary>
        /// Returns a random User-Agent string, with advanced-mode noise + bot identity tag.
        /// </summary>
        public static string GetRandomUserAgent()
        {
            var baseUa = _userAgents[GetSecureRandomIndex(_userAgents.Count)];

            if (!AdvancedModeEnabled)
                return baseUa;

            // Random noise suffix to avoid fingerprint correlation
            string noise = GenerateNoise(5);

            // Optional bot identity tag
            // const string botTag = "DiscordWhoIsBot/1.0 (+31625469+PSilverSabe@users.noreply.github.com)";

            // OS noise insertion (optional)
            string[] osVariants =
            {
                "Windows NT 10.0; Win64; x64",
                "Windows NT 10.0",
                "X11; Linux x86_64",
                "X11; Ubuntu; Linux x86_64"
            };
            string osVariant = osVariants[GetSecureRandomIndex(osVariants.Length)];

            // Rebuild UA with noisy OS string when applicable
            string finalUa = baseUa
                .Replace("Windows NT 10.0; Win64; x64", osVariant)
                .Replace("X11; Linux x86_64", osVariant);

            // Add bot tag + adv noise
            // finalUa += $"{botTag}";
            finalUa += $" (adv-{noise})";

            return finalUa;
        }

        // === Utility: cryptographically secure index ===
        private static int GetSecureRandomIndex(int max)
        {
            if (max <= 1)
                return 0;

            byte[] buffer = new byte[4];
            _secureRng.GetBytes(buffer);
            uint value = BitConverter.ToUInt32(buffer, 0);
            return (int)(value % (uint)max);
        }

        // === Utility: noise generator ===
        private static string GenerateNoise(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            char[] result = new char[length];

            byte[] buffer = new byte[length];
            _secureRng.GetBytes(buffer);

            for (int i = 0; i < length; i++)
                result[i] = chars[buffer[i] % chars.Length];

            return new string(result);
        }
    }
}
