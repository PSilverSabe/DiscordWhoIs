namespace DiscordWhoIs.HumanFakers
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;

    public static class UserAgentProvider
    {
        private static readonly RandomNumberGenerator _secureRng = RandomNumberGenerator.Create();

        // Valid modern User-Agent strings
        private static readonly List<string> _userAgents = new()
        {
            // Windows Chrome
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.6361.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",

            // Linux Chrome
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Ubuntu; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.6361.0 Safari/537.36",

            // Windows Firefox
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:118.0) Gecko/20100101 Firefox/118.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:115.0) Gecko/20100101 Firefox/115.0",

            // Linux Firefox
            "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:118.0) Gecko/20100101 Firefox/118.0",
            "Mozilla/5.0 (X11; Linux x86_64; rv:115.0) Gecko/20100101 Firefox/115.0"
        };

        /// <summary>
        /// Returns a random realistic User-Agent string
        /// </summary>
        public static string GetRandomUserAgent()
        {
            if (_userAgents.Count == 0)
                throw new InvalidOperationException("No user agents defined.");

            return _userAgents[GetSecureRandomIndex(_userAgents.Count)];
        }

        /// <summary>
        /// Securely picks a random index
        /// </summary>
        private static int GetSecureRandomIndex(int max)
        {
            if (max <= 1) return 0;

            byte[] buffer = new byte[4];
            _secureRng.GetBytes(buffer);
            uint value = BitConverter.ToUInt32(buffer, 0);
            return (int)(value % (uint)max);
        }
    }
}
