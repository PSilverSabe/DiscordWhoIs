using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Worker.Commands.Helpers;
using DiscordWhoIs.Worker.Constants;
using DiscordWhoIs.Worker.Extensions;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.Alias;

public partial class AliasCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("list", "List configured aliases")]
    public async Task ListAsync()
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
                    "You do not have permission to view aliases.",
                    _logger);
                return;
            }

            var entries = (await _store.GetAllAsync())
                .Select(e => $"{e.AliasUserName} -> {e.Author.Ao3ProfileName}")
                .ToList();

            if (entries.Count == 0)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(
                    Context.Interaction, statusLines,
                    "No aliases configured.",
                    _logger);
                return;
            }

            var sb = new StringBuilder();
            foreach (string line in entries)
            {
                if (sb.Length + line.Length + 1 > WorkerConstants.MessageMaxLength)
                {
                    await FollowupAsync($"```\n{sb}\n```", ephemeral: true);
                    sb.Clear();
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(line);
            }

            if (sb.Length > 0)
            {
                await FollowupAsync($"```\n{sb}\n```", ephemeral: true);
            }

            _logger.LogInformation("Aliases listed by {Actor}", guildUser.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occured inside /alias list command");
            throw;
        }
    }
}
