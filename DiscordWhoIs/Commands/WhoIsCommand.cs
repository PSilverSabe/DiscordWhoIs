using Discord;
using Discord.Interactions;
using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases.DataModels;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Models;
using DiscordWhoIs.Services;
using Microsoft.Extensions.Options;

namespace DiscordWhoIs.Commands
{
    public class WhoIsCommandModule(
        Ao3FicFeedService Ao3,
        IPersistentCache cache,
        ILogger<WhoIsCommandModule> logger,
        CacheConfiguration cacheOptions) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Ao3FicFeedService _Ao3 = Ao3;
        private readonly ILogger<WhoIsCommandModule> _logger = logger;
        private readonly IPersistentCache _cache = cache;
        private readonly CacheConfiguration _cacheConfig = cacheOptions;

        [SlashCommand("whoisauthor", "Fetch fics for an Ao3 user.")]
        public async Task WhoIsAsync(
            [Summary("user", "Ao3 username or configured alias")] string requested)
        {
            if (string.IsNullOrWhiteSpace(requested))
            {
                await RespondAsync("Please provide a username or alias.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            var statusLines = new List<string> { $"Resolving alias and checking cache for **{requested}**..." };
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            // Resolve alias
            var resolved = _Ao3.ResolveAo3Username(requested);
            if (!resolved.Equals(requested, StringComparison.OrdinalIgnoreCase))
            {
                statusLines.Add($"Alias **{requested}** resolved to **{resolved}**.");
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
            }

            var cacheKeyResolved = $"{resolved}";
            var throttleStatus = _Ao3.GetThrottleStatus();
            var hasCacheValue = _cache.TryGetValue(cacheKeyResolved, out _);

            if (throttleStatus.IsThrottled && !hasCacheValue)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"The Ao3 scraper is currently being throttled" +
                $" (Throttle resets in {throttleStatus.TimeUntilNextAllowed.TotalSeconds:N1} seconds)");
                return;
            }

            if (hasCacheValue)
            {
                statusLines.Add($"Cache hit for **{resolved}**, retrieving fics");
            }
            else
            {
                statusLines.Add($"No cached fics for **{resolved}**, scraping Ao3");
            }

            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            Ao3ResponseStatus fics;
            try
            {
                // Fetch fics
                fics = (await _Ao3.GetUserFicsAsync(resolved));
            }
            catch (TimeoutException tex)
            {
                _logger.LogWarning(tex, "Timeout while fetching fics for {User}", resolved);
                await ModifyOriginalResponseAsync(msg => msg.Content = $"Timeout while trying to fetch fics for **{resolved}**. Please try again later.");
                return;
            }


            if (!fics.Fics.Any() && fics.IsSuccessful)
            {
                statusLines.Add($"No fics found for **{resolved}**" +
                                (resolved.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"));
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
                return;
            }
            else if (!fics.Fics.Any() && !fics.IsSuccessful)
            {
                statusLines.Add($"Failed to fetch fics for **{resolved}**. Please try again later.");
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
                return;
            }

            // Final status line
            statusLines.Add($"Fetched {fics.Fics.Count()} fics for **{resolved}**.");
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            // Send embed as a separate normal message
            var displayName = resolved.Equals(requested, StringComparison.OrdinalIgnoreCase)
                ? resolved
                : $"{resolved} (alias: {requested})";

            var embed = new EmbedBuilder()
                .WithTitle($"Recent works for {displayName}")
                .WithDescription($"Showing up to 10 works. Cached for {_cacheConfig.ExpirationInHours.TotalHours    } hours.")
                .WithFooter("Source: Archive of Our Own")
                .WithColor(Color.DarkBlue);

            foreach (var fic in fics.Fics.Take(10))
            {
                var truncatedTitle = fic.Title.Length > 256 ? string.Concat(fic.Title.AsSpan(0, 253), "...") : fic.Title;
                embed.AddField(truncatedTitle, fic.Url, inline: false);
            }

            await Context.Channel.SendMessageAsync(embed: embed.Build());

            _logger.LogInformation("Fetched fics for {User}", requested);
        }

    }
}
