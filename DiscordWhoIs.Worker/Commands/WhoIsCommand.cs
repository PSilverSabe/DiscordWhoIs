using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker;

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
        // Build a list of individual entry strings (one per fic) so we can split into fields
        var recentEntries = new List<string>();
        var mostRecentFics = canonicalAuthor.Fanfics
            .OrderByDescending(x => x.FicLastUpdated)
            .Take(WorkerConstants.RecentWorksDefaultLimit)
            .ToList();

        foreach (Fanfic? fic in mostRecentFics)
        {
            string truncatedTitle = !string.IsNullOrEmpty(fic.Title) && fic.Title.Length > WorkerConstants.TitleMaxLength
                ? string.Concat(fic.Title.AsSpan(0, WorkerConstants.TitleMaxLength - WorkerConstants.TitleTruncateReserve), "...")
                : fic.Title ?? string.Empty;

            // Each entry is two lines: bold title + link (keeps chunking logic aligned with counts)
            recentEntries.Add($"**{truncatedTitle}**\n{fic.Link}");
        }
        _logger.LogInformation("Finished compiling recent works for {User}", canonicalAuthor.Ao3ProfileName);

        _logger.LogInformation("Building embed for {User}", canonicalAuthor.Ao3ProfileName);
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

            // Split the recent entries into chunks that respect the embed field max length.
            List<List<string>> entryChunks = SplitEntriesIntoFieldSizedChunks(recentEntries, WorkerConstants.EmbedFieldMaxLength);

            int maxRecentFieldsAllowed = Math.Max(1, WorkerConstants.EmbedMaxFields - preAddedFields);

            if (entryChunks.Count > maxRecentFieldsAllowed)
            {
                _logger.LogWarning("Recent works require {Required} fields but only {Allowed} can be used; truncating older entries.",
                    entryChunks.Count, maxRecentFieldsAllowed);

                // Keep the first allowed chunks and collapse remaining entries into the final allowed chunk.
                var limited = entryChunks.Take(maxRecentFieldsAllowed).ToList();

                if (entryChunks.Count > maxRecentFieldsAllowed)
                {
                    IEnumerable<string> overflowEntries = entryChunks.Skip(maxRecentFieldsAllowed - 1).SelectMany(c => c);
                    string overflowText = string.Join("\n\n", overflowEntries);
                    int reserve = Math.Min(WorkerConstants.EmbedFieldMaxLength, 24);
                    if (overflowText.Length > WorkerConstants.EmbedFieldMaxLength - reserve)
                    {
                        overflowText = overflowText.Substring(0, WorkerConstants.EmbedFieldMaxLength - reserve) + "...";
                    }
                    // Replace the last allowed chunk with a single chunk containing the overflow (already trimmed)
                    limited[limited.Count - 1] = new List<string> { overflowText };
                }

                entryChunks = limited;
            }

            // Add chunked recent works fields. Each field title indicates Top N and item count for that field.
            for (int i = 0; i < entryChunks.Count; i++)
            {
                List<string> chunk = entryChunks[i];
                int countInChunk = chunk.Count;
                string fieldTitle = entryChunks.Count == 1
                    ? $"Recent Works ({WorkerConstants.RecentWorksDefaultLimit} Most Recent fics)"
                    : $"Recent Works ({WorkerConstants.RecentWorksDefaultLimit} Most Recent fics) — Part {i + 1}/{entryChunks.Count}";

                string fieldValue = string.Join("\n\n", chunk);
                embed.AddField(fieldTitle, fieldValue, inline: false);

                _logger.LogInformation("Added recent works field {Index}/{Total} with {Count} entries for {User}", i + 1, entryChunks.Count, countInChunk, canonicalAuthor.Ao3ProfileName);
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

        // Local helper: split a list of per-fic entries into chunks where each chunk's combined text <= maxLen.
        static List<List<string>> SplitEntriesIntoFieldSizedChunks(List<string> entries, int maxLen)
        {
            var chunks = new List<List<string>>();
            var current = new List<string>();
            int currentLen = 0;

            foreach (string entry in entries)
            {
                // If a single entry is longer than maxLen, truncate it to fit.
                string candidate = entry;
                if (candidate.Length > maxLen)
                {
                    candidate = candidate.Substring(0, maxLen - 3) + "...";
                }

                // +2 accounts for the separator we'll use between entries ("\n\n")
                int entryLenWithSep = candidate.Length + (current.Count > 0 ? 2 : 0);

                if (currentLen + entryLenWithSep > maxLen)
                {
                    if (current.Count > 0)
                    {
                        chunks.Add(new List<string>(current));
                        current.Clear();
                        currentLen = 0;
                    }
                }

                current.Add(candidate);
                currentLen += candidate.Length + (current.Count > 1 ? 2 : 0);
            }

            if (current.Count > 0)
            {
                chunks.Add(current);
            }

            if (chunks.Count == 0)
            {
                chunks.Add(new List<string> { string.Empty });
            }

            return chunks;
        }
    }

}
