using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DiscordWhoIs.Worker.Utilities;

public static partial class Ao3LinkDetector
{
    // Matches AO3 work URLs, capturing the numeric work ID
    // e.g. https://archiveofourown.org/works/12345678
    //      https://archiveofourown.org/works/12345678/chapters/99999999
    [GeneratedRegex(
        @"https?://archiveofourown\.org/works/(?<workId>\d+)(?:/\S*)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex Ao3WorkUrlRegex();

    public static IReadOnlyList<string> ExtractLinks(string messageContent)
    {
        if (string.IsNullOrWhiteSpace(messageContent))
        {
            return [];
        }

        return [.. Ao3WorkUrlRegex()
            .Matches(messageContent)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public static IReadOnlyList<string> ExtractWorkIds(string messageContent)
    {
        if (string.IsNullOrWhiteSpace(messageContent))
        {
            return [];
        }

        return [.. Ao3WorkUrlRegex()
            .Matches(messageContent)
            .Select(m => m.Groups["workId"].Value)
            .Distinct()];
    }
}
