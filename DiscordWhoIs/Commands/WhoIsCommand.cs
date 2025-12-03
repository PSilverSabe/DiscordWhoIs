using Discord;
using Discord.Interactions;
using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases.Interfaces;

namespace DiscordWhoIs.Commands
{
    public class WhoIsCommandModule(
        IFanficRepository fanficRepository,
        IAliasRepository aliasRepository,
        ILogger<WhoIsCommandModule> logger) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IFanficRepository _fanfic = fanficRepository;
        private readonly IAliasRepository _alias = aliasRepository;
        private readonly ILogger<WhoIsCommandModule> _logger = logger;

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

            var statusLines = new List<string> { $"Resolving alias and checking database for **{requested}**..." };
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            // Resolve alias
            var hasFoundAlias = await _alias.TryResolveAsync(requested, out var real);
            if (hasFoundAlias)
            {
                statusLines.Add($"Alias **{requested}** resolved to **{real}**.");
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
            }

            // Fetch fics
            var fics = await _fanfic.GetAllByAuthorAsync(real);

            if (!fics.Any())
            {
                statusLines.Add($"No fics found for **{real}**. Please wait for the daily scrape update." +
                                (real.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"));
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
                return;
            }

            // Final status line
            statusLines.Add($"Fetched {fics.Count} fics for **{real}**.");
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            // Send embed as a separate normal message
            var displayName = hasFoundAlias ? $"{requested} (alias: {real})" : real ;

            var embed = new EmbedBuilder()
                .WithTitle($"Recent works for {displayName}")
                .WithDescription($"Showing up to 10 works.")
                .WithFooter("Source: Archive of Our Own")
                .WithColor(Color.DarkBlue);

            foreach (var fic in fics.Take(10))
            {
                var truncatedTitle = fic.Title.Length > 256 ? string.Concat(fic.Title.AsSpan(0, 253), "...") : fic.Title;
                embed.AddField(truncatedTitle, fic.Title, inline: false);
            }

            await Context.Channel.SendMessageAsync(embed: embed.Build());

            _logger.LogInformation("Fetched fics for {User}", requested);
        }

    }
}
