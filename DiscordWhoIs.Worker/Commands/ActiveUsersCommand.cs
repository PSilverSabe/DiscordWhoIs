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
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "The number of hours must be between 1 and 12.");
            return;
        }

        if (!(Context.User as IGuildUser)?.GuildPermissions.ManageMessages ?? false)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "You do not have permission to use this command. (Manage Messages required)");
            return;
        }

        if (Context.Channel is not ITextChannel channel)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "This command can only be used in a text channel.");
            return;
        }

        var activeUsers = _cache.GetActiveUsers(channel.Id, hours).ToList();

        if (activeUsers.Count == 0)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, $"No users have spoken in this channel in the last {hours} hour(s).");
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

        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, $"Users active in the last {hours} hour(s) in this channel: {messageContent}");
    }
}
