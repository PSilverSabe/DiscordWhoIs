using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
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
        string requested
    )
    {
        var statusLines = new List<string> { };
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(requested))
        {
            await RespondAsync("Please provide a username or alias.", ephemeral: true);
            _logger.LogWarning("No username or alias provided.");
            return;
        }

        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
            $"Resolving alias and checking database for **{requested}**...");

        // Resolve alias via DB lookup
        Author? canonicalAuthor = await _author.GetByAo3ProfileNameAsync(requested);

        string real;
        bool hasFoundAlias = canonicalAuthor != null && !canonicalAuthor.Ao3ProfileName.Equals(requested, StringComparison.OrdinalIgnoreCase);
        if (canonicalAuthor != null)
        {
            real = canonicalAuthor.Ao3ProfileName;
            _logger.LogInformation("Resolved {Requested} to canonical author {RealUser}", requested, real);
        }
        else
        {
            real = requested;
            _logger.LogInformation("No canonical author found for {Requested}, using as-is", requested);
        }

        // Fetch fics
        IReadOnlyList<Fanfic> fics = await _fanfic.GetAllByAuthorAsync(real);

        if (!fics.Any())
        {
            _logger.LogInformation("No fics found for {User}", real);
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                $"No fics found for **{real}**. Please wait for the daily scrape update." +
                (real.Equals(requested, StringComparison.OrdinalIgnoreCase) ? "" : $" (requested: {requested})"));
            return;
        }

        // Final status line
        statusLines.Add($"Fetched {fics.Count} fics for **{real}**.");
        _logger.LogInformation("Fetched {Count} fics for {User}", fics.Count, real);
        await ModifyOriginalResponseAsync(msg => msg.Content = string.Join("\n", statusLines));

        // Send embed as a separate normal message
        string displayName = hasFoundAlias ? $"{real} (alias: {requested})" : real;

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"Recent works for {displayName}")
            .WithDescription($"Showing up to 10 works.")
            .WithFooter("Source: Archive of Our Own")
            .WithColor(Color.DarkBlue);

        Thread.Sleep(TimeSpan.FromSeconds(1)); // Simulate processing time

        foreach (Fanfic? fic in fics.OrderByDescending(x => x.FicLastUpdated).Take(10))
        {
            string truncatedTitle = fic.Title.Length > 256 ? string.Concat(fic.Title.AsSpan(0, 253), "...") : fic.Title;
            embed.AddField(truncatedTitle, fic.Link, inline: false);
        }

        await Context.Channel.SendMessageAsync(embed: embed.Build());

        _logger.LogInformation("Fetched fics for {User}", requested);
    }

}
