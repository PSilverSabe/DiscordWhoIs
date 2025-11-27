using Discord;
using Discord.Interactions;
using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Services;
using Microsoft.Extensions.Options;

namespace DiscordWhoIs.Commands
{
    public class WhoIsCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Ao3FicFeedService _Ao3;
        private readonly ILogger<WhoIsCommandModule> _logger;
        private readonly IPersistentCache _cache;
        private readonly CacheConfiguration _cacheConfig;

        public WhoIsCommandModule(
            Ao3FicFeedService Ao3,
            IPersistentCache cache,
            ILogger<WhoIsCommandModule> logger,
            CacheConfiguration cacheOptions)
        {
            _Ao3 = Ao3;
            _logger = logger;
            _cache = cache;
            _cacheConfig = cacheOptions;
        }

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

            await Task.Delay(1000);

            var cacheKeyResolved = $"{resolved}";
            string actionLine;
            if (_cache.TryGetValue(cacheKeyResolved, out _))
            {
                actionLine = $"Cache hit for **{resolved}**, retrieving fics";
            }
            else
            {
                actionLine = $"No cached fics for **{resolved}**, scraping Ao3";
            }

            statusLines.Add(actionLine);
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            await Task.Delay(1000);

            // Animate the last line with dots
            bool ficsFetched = false;
            var loadingLineIndex = statusLines.Count - 1;
            var dots = new[] { "", ".", "..", "..." };
            var loadingTask = Task.Run(async () =>
            {
                int i = 0;
                while (!ficsFetched)
                {
                    statusLines[loadingLineIndex] = actionLine + dots[i % dots.Length];
                    await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
                    i++;
                    await Task.Delay(700);
                }
            });

            // Fetch fics
            var fics = (await _Ao3.GetUserFicsAsync(resolved)).ToList();
            ficsFetched = true;
            await loadingTask; // ensure animation stops cleanly

            if (fics.Count == 0)
            {
                statusLines.Add($"No fics found for **{resolved}**" +
                                (resolved.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"));
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
                return;
            }

            // Final status line
            statusLines.Add($"Fetched {fics.Count} fics for **{resolved}**.");
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            // Optional short delay for smoother transition
            await Task.Delay(500);

            // Send embed as a separate normal message
            var displayName = resolved.Equals(requested, StringComparison.OrdinalIgnoreCase)
                ? resolved
                : $"{resolved} (alias: {requested})";

            var embed = new EmbedBuilder()
                .WithTitle($"Recent works for {displayName}")
                .WithDescription($"Showing up to 10 works. Cached for {_cacheConfig.ExpirationInHours.Hours} hours.")
                .WithFooter("Source: Archive of Our Own")
                .WithColor(Color.DarkBlue);

            foreach (var fic in fics.Take(10))
            {
                var truncatedTitle = fic.Title.Length > 256 ? string.Concat(fic.Title.AsSpan(0, 253), "...") : fic.Title;
                embed.AddField(truncatedTitle, fic.Url, inline: false);
            }

            await Context.Channel.SendMessageAsync(embed: embed.Build());

            _logger.LogInformation("Fetched fics for {User}", requested);
        }

    }
}
