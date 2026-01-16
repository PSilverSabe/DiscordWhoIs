using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using DiscordWhoIs.Worker.Commands.Helpers;
using DiscordWhoIs.Worker.Services;
using Microsoft.EntityFrameworkCore;

namespace DiscordWhoIs.Worker.Commands;

public class ActiveUsersCommand(ActiveUsersCacheService cache) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ActiveUsersCacheService _cache = cache;

    [SlashCommand("active-users", "List users who have spoken in this channel in the last X hours (max 12).")]
    public async Task ActiveUsersAsync(
        [Summary("Hours", "Number of hours to look back (max 12)")] int hours)
    {
        var statusLines = new List<string> { };
        await DeferAsync(ephemeral: true);

        if (hours < 1 || hours > 12)
        {
            statusLines.Add("The number of hours must be between 1 and 12.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        if (!(Context.User as IGuildUser)?.GuildPermissions.ManageMessages ?? false)
        {
            statusLines.Add("You do not have permission to use this command. (Manage Messages required)");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        if (Context.Channel is not ITextChannel channel)
        {
            statusLines.Add("This command can only be used in a text channel.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        var activeUsers = _cache.GetActiveUsers(channel.Id, hours).ToList();

        if (activeUsers.Count == 0)
        {
            statusLines.Add($"No users have spoken in this channel in the last {hours} hour(s).");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        var mentions = new List<string>();
        foreach (ulong id in activeUsers)
        {
            IGuildUser user = await channel.Guild.GetUserAsync(id); // async fetch
            mentions.Add(user?.Mention ?? $"<@{id}>");
        }

        string messageContent = string.Join(", ", mentions);

        if (messageContent.Length > 2000)
        {
            messageContent = string.Concat(messageContent.AsSpan(0, 1990), "...");
        }

        statusLines.Add($"Users active in the last {hours} hour(s) in this channel: {messageContent}");
        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
    }
}
