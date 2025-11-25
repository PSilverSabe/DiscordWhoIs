using Discord;
using Discord.Interactions;
using DiscordWhoIs.Services;

namespace DiscordWhoIs.Commands
{
    public class WhoIsCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Ao3FicFeedService _ao3;
        private readonly ILogger<WhoIsCommandModule> _logger;

        public WhoIsCommandModule(Ao3FicFeedService ao3, ILogger<WhoIsCommandModule> logger)
        {
            _ao3 = ao3;
            _logger = logger;
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

            await DeferAsync(); // Give extra time for scraping

            // Resolve alias -> real AO3 username + optional description
            var (resolved, description) = _ao3.ResolveAo3UsernameWithDescription(requested);

            var fics = (await _ao3.GetUserFicsAsync(resolved)).ToList();

            if (!fics.Any())
            {
                await FollowupAsync(
                    $"No fics found for **{resolved}**" +
                    (resolved.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"),
                    ephemeral: true);
                return;
            }

            // Build up to 10 embeds, each with up to 5 fields to avoid hitting limits
            var displayName = resolved.Equals(requested, StringComparison.OrdinalIgnoreCase)
                ? resolved
                : $"{resolved} (alias: {requested})";

            var embed = new EmbedBuilder()
                .WithTitle($"Recent works for {displayName}")
                .WithDescription($"Showing up to 10 works. Cached for 24 hours." +
                                 (string.IsNullOrWhiteSpace(description) ? "" : $"\n\n{description}"))
                .WithFooter($"Source: Archive of Our Own")
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
