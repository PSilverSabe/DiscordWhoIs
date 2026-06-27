using Discord;
using Discord.Interactions;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

[Group("embed-poster", "Configure the AO3 embed poster")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class EmbedPosterCommandGroup : InteractionModuleBase<SocketInteractionContext>
{
}
