using Discord;
using Discord.Interactions;
using DiscordWhoIs.Interfaces;
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

            // Step 1: Inform user that processing is starting
            await DeferAsync(ephemeral: true);
            await FollowupAsync($"Resolving alias and checking cache for **{requested}**...");

            // Step 2: Resolve alias
            var (resolved, description) = _ao3.ResolveAo3UsernameWithDescription(requested);

            // Step 3: Inform user about resolved username
            if (!resolved.Equals(requested, StringComparison.OrdinalIgnoreCase))
            {
                await FollowupAsync($"Alias **{requested}** resolved to **{resolved}**.");
            }

            // Step 4: Inform user about cache retrieval
            var cacheKeyResolved = $"ao3_{resolved}";
            if (_cache.TryGetValue<IEnumerable<(string, string)>>(cacheKeyResolved, out _))
            {
                await FollowupAsync($"Cache hit for **{resolved}**, retrieving cached fics...");
            }
            else
            {
                await FollowupAsync($"No cached fics for **{resolved}**, scraping AO3 (this may take some time)...");
            }

            // Step 5: Fetch fics (with AO3 policy enforced)
            var fics = (await _ao3.GetUserFicsAsync(resolved)).ToList();

            if (!fics.Any())
            {
                await FollowupAsync(
                    $"No fics found for **{resolved}**" +
                    (resolved.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"),
                    ephemeral: true);
                return;
            }

            // Step 6: Prepare embed for the user
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

            await FollowupAsync(embeds: new[] { embed.Build() });

            _logger.LogInformation("Fetched fics for {User}", requested);
        }
    }
}
