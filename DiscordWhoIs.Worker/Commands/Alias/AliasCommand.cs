using Discord.Interactions;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.Alias;

[Group("alias", "Manage Ao3 aliases")]
public partial class AliasCommand(
    IAliasRepository store,
    ILogger<AliasCommand> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAliasRepository _store = store;
    private readonly ILogger<AliasCommand> _logger = logger;
}
