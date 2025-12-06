using Discord;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordWhoIs.Worker.Commands
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
            await DeferAsync(ephemeral: true);

            if (string.IsNullOrWhiteSpace(requested))
            {
                await RespondAsync("Please provide a username or alias.", ephemeral: true);
                _logger.LogWarning("No username or alias provided.");
                return;
            }

            var statusLines = new List<string> { $"Resolving alias and checking database for **{requested}**..." };
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            // Resolve alias
            var hasFoundAlias = await _alias.TryResolveAsync(requested, out var real);
            if (hasFoundAlias)
            {
                _logger.LogInformation("Resolved alias {Alias} to {RealUser}", requested, real);
            }
            else
            {
                real = requested;
                _logger.LogInformation("No alias found for {Requested}, using as-is", requested);
            }

            // Fetch fics
            var fics = await _fanfic.GetAllByAuthorAsync(real);

            if (!fics.Any())
            {
                statusLines.Add($"No fics found for **{real}**. Please wait for the daily scrape update." +
                                (real.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"));
                _logger.LogInformation("No fics found for {User}", real);
                await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));
                return;
            }

            // Final status line
            statusLines.Add($"Fetched {fics.Count} fics for **{real}**.");
            _logger.LogInformation("Fetched {Count} fics for {User}", fics.Count, real);
            await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

            // Send embed as a separate normal message
            var displayName = hasFoundAlias ? $"{real} (alias: {requested})" : real;

            var embed = new EmbedBuilder()
                .WithTitle($"Recent works for {displayName}")
                .WithDescription($"Showing up to 10 works.")
                .WithFooter("Source: Archive of Our Own")
                .WithColor(Color.DarkBlue);

            Thread.Sleep(TimeSpan.FromSeconds(1)); // Simulate processing time

            foreach (var fic in fics.OrderByDescending(x => x.FicLastUpdated).Take(10))
            {
                var truncatedTitle = fic.Title.Length > 256 ? string.Concat(fic.Title.AsSpan(0, 253), "...") : fic.Title;
                embed.AddField(truncatedTitle, fic.Link, inline: false);
            }

            await Context.Channel.SendMessageAsync(embed: embed.Build());

            _logger.LogInformation("Fetched fics for {User}", requested);
        }

    }
}
