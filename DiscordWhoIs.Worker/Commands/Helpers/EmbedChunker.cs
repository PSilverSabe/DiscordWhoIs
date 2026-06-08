using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordWhoIs.Worker.Commands.Helpers;

/// <summary>
/// Helper to split a list of per-item strings into embed-safe field chunks.
/// Each chunk is a list of entries whose joined text will not exceed the provided max field length.
/// If the resulting chunk count exceeds <paramref name="maxFieldsAllowed"/>, the remaining entries
/// are collapsed into the final allowed chunk and trimmed to fit.
/// </summary>
/// <param name="entries">List of per-item strings to chunk into embed fields.</param>
/// <param name="maxFieldLength">Maximum length of each embed field.</param>
/// <param name="maxFieldsAllowed">Maximum number of embed fields allowed.</param>
public static class EmbedChunker
{
    public static List<List<string>> ChunkEntries(
        List<string> entries,
        int maxFieldLength,
        int maxFieldsAllowed)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFieldLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFieldsAllowed);

        var chunks = new List<List<string>>();
        var current = new List<string>();
        int currentLen = 0;

        foreach (string raw in entries)
        {
            // Ensure single entry is not longer than the max field length.
            string entry = raw;
            if (entry.Length > maxFieldLength)
            {
                entry = string.Concat(entry.AsSpan(0, maxFieldLength - 3), "...");
            }

            // Length with separator ("\n\n") when not first in current.
            int entryLenWithSep = entry.Length + (current.Count > 0 ? 2 : 0);

            if (currentLen + entryLenWithSep > maxFieldLength)
            {
                if (current.Count > 0)
                {
                    chunks.Add([.. current]);
                    current.Clear();
                    currentLen = 0;
                }
            }

            current.Add(entry);
            currentLen += entry.Length + (current.Count > 1 ? 2 : 0);
        }

        if (current.Count > 0)
        {
            chunks.Add(current);
        }

        // If we fit within allowed fields, return.
        if (chunks.Count <= maxFieldsAllowed)
        {
            return chunks;
        }

        // Otherwise, keep first (maxFieldsAllowed - 1) chunks and collapse remainder into last chunk.
        var result = chunks.Take(maxFieldsAllowed - 1).Select(c => new List<string>(c)).ToList();

        var remainingEntries = chunks.Skip(maxFieldsAllowed - 1).SelectMany(c => c).ToList();
        string combined = string.Join("\n\n", remainingEntries);

        // Trim combined overflow to fit into a single field, reserve a few chars for ellipsis.
        int reserve = 3;
        if (combined.Length > maxFieldLength)
        {
            combined = string.Concat(combined.AsSpan(0, Math.Max(0, maxFieldLength - reserve)), "...");
        }

        result.Add([combined]);

        return result;
    }
}
