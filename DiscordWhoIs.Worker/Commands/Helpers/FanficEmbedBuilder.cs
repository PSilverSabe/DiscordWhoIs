using System;
using System.Linq;
using Discord;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Worker.Constants;

namespace DiscordWhoIs.Worker.Commands.Helpers;

public static class FanficEmbedBuilder
{
    public static Embed Build(Fanfic fic)
    {
        string title = fic.Title.Length > WorkerConstants.TitleMaxLength
            ? string.Concat(fic.Title.AsSpan(0, WorkerConstants.TitleMaxLength - WorkerConstants.TitleTruncateReserve), "...")
            : fic.Title;

        string authors = fic.Authors.Count > 0
            ? string.Join(", ", fic.Authors.Select(a =>
                $"[{a.Ao3ProfileName}]({string.Format(WorkerConstants.Ao3ProfileUrlFormat, a.Ao3ProfileName)})"))
            : "Unknown";

        // Summaries can exceed embed limits - truncate to fit within field limit
        string summary = fic.Summary.Length > WorkerConstants.EmbedFieldMaxLength
            ? string.Concat(fic.Summary.AsSpan(0, WorkerConstants.EmbedFieldMaxLength - 3), "...")
            : fic.Summary;

        return new EmbedBuilder()
            .WithTitle(title)
            .WithUrl(fic.Link)
            .WithDescription(summary)
            .AddField("Author(s)", authors, inline: true)
            .AddField("Rating", fic.Rating, inline: true)
            .AddField("Category", fic.Category, inline: true)
            .AddField("Words", fic.WordCount.ToString("N0"), inline: true)
            .AddField("Chapters", fic.ChapterCount.ToString("N0"), inline: true)
            .AddField("Kudos", fic.KudosCount.ToString("N0"), inline: true)
            .AddField("Hits", fic.HitCount.ToString("N0"), inline: true)
            .AddField("Bookmarks", fic.BookmarksCount.ToString("N0"), inline: true)
            .AddField("Comments", fic.CommentCount.ToString("N0"), inline: true)
            .AddField("Warnings", fic.Warnings, inline: false)
            .AddField("Last Updated", fic.FicLastUpdated.ToString("dd MMM yyyy"), inline: true)
            .WithFooter(WorkerConstants.Ao3FooterText, WorkerConstants.Ao3FooterIcon)
            .WithColor(Color.DarkRed)
            .Build();
    }
}
