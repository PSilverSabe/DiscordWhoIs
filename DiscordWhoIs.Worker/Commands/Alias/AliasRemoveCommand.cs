using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.Alias;

public class AliasRemoveCommand(
    IAliasRepository store,
    ILogger<AliasRemoveCommand> logger)
    : AliasCommandGroup
{
    private readonly IAliasRepository _store = store;
    private readonly ILogger<AliasRemoveCommand> _logger = logger;

    [SlashCommand("remove", "Remove an alias")]
    public async Task RemoveAsync(
        [Summary("Alias", "Alias name to remove")]
        string alias)
    {
        var statusLines = new List<string>();
        await DeferAsync(ephemeral: true);

        if (Context.User is not SocketGuildUser guildUser)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                "This command must be used in a server (guild).",
                _logger);
            return;
        }

        if (!guildUser.HasAdminPermissions())
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                "You do not have permission to manage aliases.",
                _logger);
            return;
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                "`alias` is required.",
                _logger);
            return;
        }

        try
        {
            bool removed = await _store.RemoveAsync(alias);

            string message = removed
                ? $"Removed alias `{alias}`."
                : $"Alias `{alias}` not found.";

            if (removed)
            {
                _logger.LogInformation("Alias removed by {Actor}: {Alias}", guildUser.Username, alias);
            }

            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines, message, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove alias {Alias}", alias);
            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                "Failed to remove alias due to an internal error.",
                _logger);
        }
    }
}
