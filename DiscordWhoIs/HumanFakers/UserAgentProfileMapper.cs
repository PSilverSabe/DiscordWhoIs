namespace DiscordWhoIs.HumanFakers
{
    using System;
    using System.Collections.Generic;

    public static class UserAgentProfileMapper
    {
        private static readonly Dictionary<string, (string Locale, string AcceptLanguage, string Timezone)> RegionProfiles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["US"] = ("en-US", "en-US,en;q=0.9", "America/New_York"),
                ["UK"] = ("en-GB", "en-GB,en;q=0.9", "Europe/London"),
                ["AU"] = ("en-AU", "en-AU,en;q=0.9", "Australia/Sydney"),
                ["CA"] = ("en-CA", "en-CA,en;q=0.9", "America/Toronto"),
                ["FR"] = ("fr-FR", "fr-FR,fr;q=0.9,en;q=0.8", "Europe/Paris"),
                ["DE"] = ("de-DE", "de-DE,de;q=0.9,en;q=0.8", "Europe/Berlin"),
                ["JP"] = ("ja-JP", "ja-JP,ja;q=0.9,en;q=0.5", "Asia/Tokyo")
            };

        /// <summary>
        /// Returns a locale, Accept-Language, and timezone that match a typical user
        /// using the given User-Agent string. The mapping is heuristic but realistic.
        /// </summary>
        public static (string Locale, string AcceptLanguage, string TimezoneId) MapFromUserAgent(string ua)
        {
            if (string.IsNullOrWhiteSpace(ua))
                return ("en-US", "en-US,en;q=0.9", "America/New_York");

            ua = ua.ToLowerInvariant();

            // 1. Region-based patterns
            if (ua.Contains("en-gb") || ua.Contains("windows nt") && ua.Contains("; gb"))
                return RegionProfiles["UK"];

            if (ua.Contains("macintosh") && ua.Contains("mac os x 10_15") && ua.Contains("en-gb"))
                return RegionProfiles["UK"];

            if (ua.Contains("en-au") || ua.Contains(" x64; au") || ua.Contains("; aus"))
                return RegionProfiles["AU"];

            if (ua.Contains("en-ca") || ua.Contains("windows nt") && ua.Contains("ca;"))
                return RegionProfiles["CA"];

            if (ua.Contains("fr-fr") || ua.Contains("; fr"))
                return RegionProfiles["FR"];

            if (ua.Contains("de-de") || ua.Contains("; de"))
                return RegionProfiles["DE"];

            if (ua.Contains("ja-jp") || ua.Contains("macintosh; intel mac os x 10_"))
                return RegionProfiles["JP"];

            // 2. If UA mentions Chrome/Edge/Safari but no region, guess US
            if (ua.Contains("chrome") || ua.Contains("safari") || ua.Contains("edg"))
                return RegionProfiles["US"];

            // 3. Fallback (most global browsers default to en-US)
            return RegionProfiles["US"];
        }
    }
}
