using Discord;
using Discord.Interactions;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Services;

namespace DiscordWhoIs.Commands
{
    public class WhoIsCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Ao3FicFeedService _ao3;
        private readonly ILogger<WhoIsCommandModule> _logger;
        private readonly TimeSpan _cacheLength;
        private readonly IPersistentCache _cache;

        public WhoIsCommandModule(
            Ao3FicFeedService ao3,
            IPersistentCache cache,
            ILogger<WhoIsCommandModule> logger,
            IConfiguration configuration)
        {
            _ao3 = ao3;
            _logger = logger;
            _cache = cache;

            if (int.TryParse(configuration?["Cache:ExpirationInHours"], out var cl) && cl > 0)
                _cacheLength = TimeSpan.FromHours(cl);
            else
                _cacheLength = TimeSpan.FromHours(12);
        }

        [SlashCommand("whoisauthor", "Fetch fics for an AO3 user.")]
        public async Task WhoIsAsync(
            [Summary("user", "AO3 username or configured alias")] string requested)
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
            var (resolved, description) = _ao3.ResolveAo3UsernameWithDescription(requested);
            if (!resolved.Equals(requested, StringComparison.OrdinalIgnoreCase))
            {
                statusLines.Add($"Alias **{requested}** resolved to **{resolved}**.");
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
            }

            await Task.Delay(1000);

            var cacheKeyResolved = $"ao3_{resolved}";
            string actionLine;
            if (_cache.TryGetValue<IEnumerable<(string, string)>>(cacheKeyResolved, out _))
            {
                actionLine = $"Cache hit for **{resolved}**, retrieving fics";
            }
            else
            {
                actionLine = $"No cached fics for **{resolved}**, scraping AO3";
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
            var fics = (await _ao3.GetUserFicsAsync(resolved)).ToList();
            ficsFetched = true;
            await loadingTask; // ensure animation stops cleanly

            if (!fics.Any())
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
                .WithDescription($"Showing up to 10 works. Cached for {_cacheLength.TotalHours} hours." +
                                 (string.IsNullOrWhiteSpace(description) ? "" : $"\n\n{description}"))
                .WithFooter("Source: Archive of Our Own")
                .WithColor(Color.DarkBlue);

            foreach (var (title, url) in fics.Take(10))
            {
                var truncatedTitle = title.Length > 256 ? title.Substring(0, 253) + "..." : title;
                embed.AddField(truncatedTitle, url, inline: false);
            }

            await Context.Channel.SendMessageAsync(embed: embed.Build());

            await Task.Delay(500);

            await DeleteOriginalResponseAsync();

            _logger.LogInformation("Fetched fics for {User}", requested);
        }

    }
}
