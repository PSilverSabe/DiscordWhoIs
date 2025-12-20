using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Worker.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordWhoIs.Worker.Commands
{
    public class ActiveUsersModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ActiveUsersCacheService _cache;

        public ActiveUsersModule(ActiveUsersCacheService cache)
        {
            _cache = cache;
        }

        [SlashCommand("activeusers", "List users who have spoken in this channel in the last X hours (max 12).")]
        public async Task ActiveUsersAsync(
            [Summary("hours", "Number of hours to look back (max 12)")] int hours)
        {
            if (hours < 1 || hours > 12)
            {
                await RespondAsync("Please provide a number between 1 and 12 hours.", ephemeral: true);
                return;
            }

            if (!(Context.User as IGuildUser)?.GuildPermissions.ManageMessages ?? false)
            {
                await RespondAsync("You do not have permission to use this command.", ephemeral: true);
                return;
            }

            var channel = Context.Channel as ITextChannel;
            if (channel == null)
            {
                await RespondAsync("This command can only be used in a text channel.", ephemeral: true);
                return;
            }

            var activeUsers = _cache.GetActiveUsers(channel.Id, hours).ToList();

            if (!activeUsers.Any())
            {
                await RespondAsync($"No users have spoken in this channel in the last {hours} hour(s).", ephemeral: true);
                return;
            }

            var mentions = new List<string>();
            foreach (var id in activeUsers)
            {
                var user = await channel.Guild.GetUserAsync(id); // async fetch
                mentions.Add(user?.Mention ?? $"<@{id}>");
            }

            var messageContent = string.Join(", ", mentions);

            if (messageContent.Length > 2000)
            {
                messageContent = messageContent.Substring(0, 1990) + "...";
            }

            await RespondAsync($"Users active in the last {hours} hour(s) in this channel: {messageContent}");
        }
    }
}
