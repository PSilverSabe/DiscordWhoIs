using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

namespace DiscordWhoIs.Worker.Extensions;

/// <summary>
/// Extension methods for IMemoryCache.
/// </summary>
public static class MemoryCacheExtensions
{
    /// <summary>
    /// Gets all cache keys from the memory cache.
    /// </summary>
    public static IEnumerable<string> GetCacheKeys(this IMemoryCache cache)
    {
        if (cache is not MemoryCache memoryCache)
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            object? coherentState = typeof(MemoryCache)
                .GetProperty("CoherentState",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(memoryCache);

            if (coherentState == null)
            {
                return Enumerable.Empty<string>();
            }

            FieldInfo? entriesField = coherentState.GetType()
                .GetField("_entries",
                    BindingFlags.NonPublic | BindingFlags.Instance);

            if (entriesField?.GetValue(coherentState) is not ConcurrentDictionary<object, object> entries)
            {
                return Enumerable.Empty<string>();
            }

            return entries.Keys
                .OfType<string>()
                .ToList();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }
}
