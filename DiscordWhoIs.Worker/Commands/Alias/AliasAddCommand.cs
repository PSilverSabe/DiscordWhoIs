using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Worker.Commands.Helpers;
using DiscordWhoIs.Worker.Extensions;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.Alias;

public partial class AliasCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("add", "Add or update an alias")]
    public async Task AddAsync(
        [Summary("Alias", "Alias name")]
        string alias,
        [Summary("Ao3-Username", "Ao3 username or configured alias")]
        string user)
    {
        try
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

            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(user))
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    "Both `alias` and `user` are required.",
                    _logger);
                return;
            }

            await _store.AddOrUpdateAsync(alias, user);

            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                $"Added/updated alias ``{alias}`` -> ``{user}``",
                _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add/update alias {Alias} -> {User}", alias, user);
            throw;
        }
    }
}
