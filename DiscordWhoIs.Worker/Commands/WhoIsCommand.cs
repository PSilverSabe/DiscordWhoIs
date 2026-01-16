using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands;

public class WhoIsCommandModule(
    IFanficRepository fanficRepository,
    IAuthorRepository authorRepository,
    ILogger<WhoIsCommandModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IFanficRepository _fanfic = fanficRepository;
    private readonly IAuthorRepository _author = authorRepository;
    private readonly ILogger<WhoIsCommandModule> _logger = logger;

    [SlashCommand("who-is-author", "Fetch fics for an Ao3 user.")]
    public async Task WhoIsAsync(
        [Summary("Ao3-Username", "Ao3 username or configured alias")]
        string? requested = null,
        [Summary("Discord-Username", "Discord username for the author")]
        SocketGuildUser? user = null
    )
    {
        var statusLines = new List<string> { };
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(requested) && user == null)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                "Please provide either an Ao3 Name, Ao3 Alias, or a Discord User in order to get Author Information.", _logger);
            return;
        }

        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
            $"Resolving alias and checking database for **{requested}**...", _logger);

        Author? canonicalAuthor = null;
        if (user != null)
        {
            canonicalAuthor = await _author.GetByDiscordIdAsync(user.Id);
        }
        else
        {
            requested = requested!.Trim();

            if (string.IsNullOrWhiteSpace(requested))
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                    "Provided Ao3 username/alias is empty after trimming whitespace.", _logger);
                return;
            }

            canonicalAuthor = await _author.GetByAo3ProfileNameAsync(requested);
        }

        if (canonicalAuthor == null)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                $"No Ao3 author found for username/alias/discord user **{requested}**.", _logger);
            return;
        }

        if (canonicalAuthor.Fanfics.Count == 0)
        {
            _logger.LogInformation("No fics found for {User}", canonicalAuthor.Ao3ProfileName);
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                $"No fics found for **{canonicalAuthor.Ao3ProfileName}**. Please wait for the daily scrape update." +
                (canonicalAuthor.Ao3ProfileName.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"),
                _logger);
            return;
        }

        // Final status line
        statusLines.Add($"Fetched {canonicalAuthor.Fanfics.Count} fics for **{canonicalAuthor.Ao3ProfileName}**.");
        _logger.LogInformation("Fetched {Count} fics for {User}", canonicalAuthor.Fanfics.Count, canonicalAuthor.Ao3ProfileName);
        await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

        // Send embed as a separate normal message
        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"Recent works for {canonicalAuthor.Ao3ProfileName}")
            .WithDescription($"Showing up to 10 works.")
            .WithFooter("Source: Archive of Our Own")
            .WithColor(Color.DarkBlue);

        Thread.Sleep(TimeSpan.FromSeconds(1)); // Simulate processing time

        foreach (Fanfic? fic in canonicalAuthor.Fanfics.OrderByDescending(x => x.FicLastUpdated).Take(10))
        {
            string truncatedTitle = fic.Title.Length > 256 ? string.Concat(fic.Title.AsSpan(0, 253), "...") : fic.Title;
            embed.AddField(truncatedTitle, fic.Link, inline: false);
        }

        await Context.Channel.SendMessageAsync(embed: embed.Build());

        _logger.LogInformation("Fetched fics for {User}", requested);
    }

}
