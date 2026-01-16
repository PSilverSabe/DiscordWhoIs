using Discord.WebSocket;

namespace DiscordWhoIs.Worker.Commands.Helpers;

public static class SocketGuildUserExtensions
{
    extension(SocketGuildUser guildUser)
    {
        public bool HasAdminPermissions() => guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageGuild;
    }
}
