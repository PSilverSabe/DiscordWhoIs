using Discord.WebSocket;

namespace DiscordWhoIs.Worker.Extensions;

public static class SocketGuildUserExtensions
{
    extension(SocketGuildUser guildUser)
    {
        public bool HasAdminPermissions() => guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageGuild;
    }
}
