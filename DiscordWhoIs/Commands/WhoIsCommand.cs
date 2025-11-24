using DiscordWhoIs.Commands;

namespace DiscordWhoIs.Commands
{
    using Discord;
    using Discord.WebSocket;
    using DiscordWhoIs.Interfaces;
    using DiscordWhoIs.Services;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class WhoIsCommand : ISlashCommand
    {
        private readonly Ao3FicFeedService _ao3;
        private readonly ILogger<WhoIsCommand> _logger;

        public WhoIsCommand(Ao3FicFeedService ao3, ILogger<WhoIsCommand> logger)
        {
            _ao3 = ao3;
            _logger = logger;
        }

        public string Name => "whoisauthor";

        public ApplicationCommandProperties Build()
        {
            return new SlashCommandBuilder()
                .WithName(Name)
                .WithDescription("Fetch fics for an AO3 user.")
                .AddOption("user", ApplicationCommandOptionType.String, "AO3 username or configured alias", isRequired: true)
                .Build();
        }

        public async Task ExecuteAsync(SocketSlashCommand command)
        {
            var requested = command.Data.Options.First().Value!.ToString()!;

            await command.DeferAsync(); // Give us extra time for scraping

            // Resolve alias -> real AO3 username and description for display and querying
            var (resolved, description) = _ao3.ResolveAo3UsernameWithDescription(requested);

            var fics = (await _ao3.GetUserFicsAsync(resolved)).ToList();

            if (!fics.Any())
            {
                await command.FollowupAsync($"No fics found for **{resolved}**{(resolved.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})")}.");
                return;
            }

            // Build up to 10 embeds, each with up to 5 fields to avoid hitting limits
            var displayName = resolved.Equals(requested, StringComparison.OrdinalIgnoreCase) ? resolved : $"{resolved} (alias: {requested})";

            var embed = new EmbedBuilder()
                .WithTitle($"Recent works for {displayName}")
                .WithDescription($"Showing up to 10 works. Cached for 24 Hours." + (string.IsNullOrWhiteSpace(description) ? "" : $"\n\n{description}"))
                .WithFooter($"Source: Archive of Our Own")
                .WithColor(Color.DarkBlue);

            foreach (var (title, url) in fics.Take(10))
            {
                embed.AddField(title.Length > 256 ? title.Substring(0, 253) + "..." : title, url, inline: false);
            }

            await command.FollowupAsync(embeds: new[] { embed.Build() });
        }
    }
}