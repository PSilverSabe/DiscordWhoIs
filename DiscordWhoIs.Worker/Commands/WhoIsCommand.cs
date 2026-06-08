using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        _logger.LogInformation("Compiling Title and Link for top {Limit} most recently updated fics for {User}",
            WorkerConstants.RecentWorksDefaultLimit, canonicalAuthor.Ao3ProfileName);

        var compiledString = new StringBuilder();
        foreach (Fanfic? fic in canonicalAuthor.Fanfics.OrderByDescending(x => x.FicLastUpdated).Take(WorkerConstants.RecentWorksDefaultLimit))
        {
            _logger.LogInformation("Processing fic: {Title} ({Link})", fic.Title, fic.Link);

            // Truncate title using constants
            string truncatedTitle;
            if (!string.IsNullOrEmpty(fic.Title) && fic.Title.Length > WorkerConstants.TitleMaxLength)
            {
                truncatedTitle = string.Concat(fic.Title.AsSpan(0, WorkerConstants.TitleMaxLength - WorkerConstants.TitleTruncateReserve), "...");
            }
            else
            {
                truncatedTitle = fic.Title ?? string.Empty;
            }

            compiledString.AppendLine($"**{truncatedTitle}**");
            compiledString.AppendLine($"{fic.Link}\n");
        }
        _logger.LogInformation("Finished compiling recent works for {User}", canonicalAuthor.Ao3ProfileName);

        Embed? embedBuilt = null;
        try
        {
            // Build embed and track how many fields we add before adding 'Recent Works' chunks.
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle($"Author Profile: {canonicalAuthor.Ao3ProfileName}");

            int preAddedFields = 0;

            embed.AddField("Total Works",
                $"{canonicalAuthor.Fanfics.Count}",
                inline: true);
            preAddedFields++;

            embed.AddField("Total Kudos",
                $"{canonicalAuthor.Fanfics.Sum(x => x.KudosCount)}",
                inline: true);
            preAddedFields++;

            embed.AddField("Total Hits",
                $"{canonicalAuthor.Fanfics.Sum(x => x.HitCount)}",
                inline: true);
            preAddedFields++;

            embed.AddField("Ao3 Profile",
                string.Format(WorkerConstants.Ao3ProfileUrlFormat, canonicalAuthor.Ao3ProfileName),
                inline: false);
            preAddedFields++;

            embed.AddField("Description",
                $"{canonicalAuthor.Description ?? "No description for author."}\n\n",
                inline: false);
            preAddedFields++;

            // Discord embed field value max length is defined in constants.
            string recentText = compiledString.ToString();
            List<string> recentChunks = SplitTextIntoFieldSizedChunks(recentText, WorkerConstants.EmbedFieldMaxLength);

            int maxRecentFieldsAllowed = Math.Max(1, WorkerConstants.EmbedMaxFields - preAddedFields);

            if (recentChunks.Count > maxRecentFieldsAllowed)
            {
                _logger.LogWarning("Recent works require {Required} fields but only {Allowed} can be used; truncating most older entries.",
                    recentChunks.Count, maxRecentFieldsAllowed);

                // Keep the allowed number of chunks and collapse overflow into the final field.
                var limited = recentChunks.Take(maxRecentFieldsAllowed).ToList();

                if (recentChunks.Count > maxRecentFieldsAllowed)
                {
                    // Combine the overflow chunks into a single string and trim to fit into a field.
                    string overflow = string.Join("\n", recentChunks.Skip(maxRecentFieldsAllowed - 1));
                    // Reserve a small amount for ellipsis; use constants for max length.
                    int reserve = Math.Min(WorkerConstants.EmbedFieldMaxLength, 24);
                    if (overflow.Length > WorkerConstants.EmbedFieldMaxLength - reserve)
                    {
                        overflow = string.Concat(overflow.AsSpan(0, WorkerConstants.EmbedFieldMaxLength - reserve), "...");
                    }
                    limited[limited.Count - 1] = overflow;
                }

                recentChunks = limited;
            }

            // Add chunked recent works fields
            for (int i = 0; i < recentChunks.Count; i++)
            {
                string fieldTitle = recentChunks.Count == 1
                    ? $"Recent Works ({WorkerConstants.RecentWorksDefaultLimit} Most Recent)"
                    : $"Recent Works ({i + 1}/{recentChunks.Count})";
                embed.AddField(fieldTitle, recentChunks[i], inline: false);
            }

            embed = embed.WithFooter(WorkerConstants.Ao3FooterText, WorkerConstants.Ao3FooterIcon)
                         .WithColor(Color.DarkBlue);

            embedBuilt = embed.Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while building embed for {User}", canonicalAuthor.Ao3ProfileName);
            throw;
        }

        if (embedBuilt != null)
        {
            await Context.Channel.SendMessageAsync(embed: embedBuilt);
            _logger.LogInformation("Fetched fics for {User}", requested);
        }
        else
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context.Interaction, statusLines,
                $"Failed to build embed for **{canonicalAuthor.Ao3ProfileName}**. Please try again later.", _logger);
            _logger.LogWarning("Embed was null for {User}, skipping sending embed.", canonicalAuthor.Ao3ProfileName);
        }

        // Local helper: split text by line boundaries into chunks where each chunk <= maxLen.
        static List<string> SplitTextIntoFieldSizedChunks(string text, int maxLen)
        {
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var chunks = new List<string>();
            var current = new StringBuilder();

            foreach (string line in lines)
            {
                // If a single line is longer than maxLen, truncate the line itself.
                string candidateLine = line;
                if (candidateLine.Length > maxLen)
                {
                    candidateLine = candidateLine.Substring(0, maxLen - 3) + "...";
                }

                // If adding this line would exceed maxLen, push current chunk and start new.
                if (current.Length + candidateLine.Length + 1 > maxLen)
                {
                    if (current.Length > 0)
                    {
                        chunks.Add(current.ToString().TrimEnd());
                        current.Clear();
                    }
                }

                current.AppendLine(candidateLine);
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString().TrimEnd());
            }

            // Ensure at least one chunk
            if (chunks.Count == 0)
            {
                chunks.Add(string.Empty);
            }

            return chunks;
        }
    }

}
