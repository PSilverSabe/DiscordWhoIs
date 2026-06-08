using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        // Validation: require exactly one
        if (string.IsNullOrWhiteSpace(requested) && user is null)
        {
            await RespondAsync(
                "You must provide **either** an AO3 username **or** a Discord user.",
                ephemeral: true);
            return;
        }

        var statusLines = new List<string> { };
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(requested) && user == null)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context.Interaction, statusLines,
                "Please provide either an Ao3 Name, Ao3 Alias, or a Discord User in order to get Author Information.", _logger);
            return;
        }

        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context.Interaction, statusLines,
            $"Resolving alias and checking database for user...", _logger);

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
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context.Interaction, statusLines,
                    "Provided Ao3 username/alias is empty after trimming whitespace.", _logger);
                return;
            }

            canonicalAuthor = await _author.GetByAo3ProfileNameAsync(requested);
        }

        if (canonicalAuthor == null)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context.Interaction, statusLines,
                $"No Ao3 author found for username/alias/discord user.", _logger);
            return;
        }

        if (canonicalAuthor.Fanfics.Count == 0)
        {
            _logger.LogInformation("No fics found for {User}", canonicalAuthor.Ao3ProfileName);

            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context.Interaction, statusLines,
                $"No fics found for **{canonicalAuthor.Ao3ProfileName}**. Please wait for the daily scrape update." +
                (canonicalAuthor.Ao3ProfileName.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"),
                _logger);
            return;
        }

        // Final status line
        _logger.LogInformation("Fetched {Count} fics for {User}", canonicalAuthor.Fanfics.Count, canonicalAuthor.Ao3ProfileName);

        statusLines.Add($"Fetched {canonicalAuthor.Fanfics.Count} fics for **{canonicalAuthor.Ao3ProfileName}**.");
        await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

        _logger.LogInformation("Compiling Title and Link for top 10 most recently updated fics for {User}", canonicalAuthor.Ao3ProfileName);
        var compiledString = new StringBuilder();
        foreach (Fanfic? fic in canonicalAuthor.Fanfics.OrderByDescending(x => x.FicLastUpdated).Take(10))
        {
            _logger.LogInformation("Processing fic: {Title} ({Link})", fic.Title, fic.Link);
            string truncatedTitle = fic.Title.Length > 256 ? string.Concat(fic.Title.AsSpan(0, 253), "...") : fic.Title;
            compiledString.AppendLine($"**{truncatedTitle}**");
            compiledString.AppendLine($"{fic.Link}\n");
        }
        _logger.LogInformation("Finished compiling recent works for {User}", canonicalAuthor.Ao3ProfileName);

        _logger.LogInformation("Preparing embed for {User}", canonicalAuthor.Ao3ProfileName);
        _logger.LogInformation("Author: {Author}", canonicalAuthor.Ao3ProfileName);
        _logger.LogInformation("Total Words: {Words}", canonicalAuthor.Fanfics.Sum(x => x.WordCount));
        _logger.LogInformation("Total Kudos: {Kudos}", canonicalAuthor.Fanfics.Sum(x => x.KudosCount));
        _logger.LogInformation("Total Hits: {Hits}", canonicalAuthor.Fanfics.Sum(x => x.HitCount));
        _logger.LogInformation("Description: {Description}", canonicalAuthor.Description ?? "No description for author.");
        _logger.LogInformation("Recent Works (10 Most Recent):\n{Works}",
            string.Join("\n", canonicalAuthor.Fanfics.OrderByDescending(x => x.FicLastUpdated).Take(10)
                .Select(x => $"{x.Title} ({x.Link})")));
        // Send embed as a separate normal message
        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"Author Profile: {canonicalAuthor.Ao3ProfileName}")
            .AddField("Total Works",
                $"{canonicalAuthor.Fanfics.Count}",
                inline: true)
            .AddField("Total Kudos",
                $"{canonicalAuthor.Fanfics.Sum(x => x.KudosCount)}",
                inline: true)
            .AddField("Total Hits",
                $"{canonicalAuthor.Fanfics.Sum(x => x.HitCount)}",
                inline: true)
            .AddField("Ao3 Profile",
                $"https://archiveofourown.org/users/{canonicalAuthor.Ao3ProfileName}",
                inline: false)
            .AddField("Description",
                $"{canonicalAuthor.Description ?? "No description for author."}\n\n",
                inline: false)
            .AddField("Recent Works (10 Most Recent)",
                compiledString.ToString(),
                inline: false)
            .WithFooter("Source: Archive of Our Own", "https://archiveofourown.org/images/ao3_logos/logo_42.png")
            .WithColor(Color.DarkBlue);

        Thread.Sleep(TimeSpan.FromSeconds(1)); // Simulate processing time

        await Context.Channel.SendMessageAsync(embed: embed.Build());

        _logger.LogInformation("Fetched fics for {User}", requested);
    }

}
