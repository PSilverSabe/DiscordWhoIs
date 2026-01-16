using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands;

[Group("alias", "Manage Ao3 aliases")]
public class AliasCommandModule(
    IAliasRepository store,
    ILogger<AliasCommandModule> logger)
        : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAliasRepository _store = store;
    private readonly ILogger<AliasCommandModule> _logger = logger;

    // ----- ADD SUBCOMMAND -----
    [SlashCommand("add", "Add or update an alias")]
    public async Task AddAsync(
        [Summary("Alias", "Alias name")]
        string alias,
        [Summary("Ao3-Username", "Ao3 username or configured alias")]
        string user
    )
    {
        var statusLines = new List<string> { };

        await DeferAsync(ephemeral: true);

        if (Context.User is not SocketGuildUser guildUser)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "This command must be used in a server (guild).");
            return;
        }

        if (guildUser.HasAdminPermissions())
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "You do not have permission to manage aliases.");
            return;
        }

        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(user))
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "Both `alias` and `user` are required.");
            return;
        }

        await _store.AddOrUpdateAsync(alias, user);

        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, $"Added/updated alias ``{alias}`` -> ``{user}``");
        return;
    }

    // ----- REMOVE SUBCOMMAND -----
    [SlashCommand("remove", "Remove an alias")]
    public async Task RemoveAsync(
        [Summary("Alias", "Alias name to remove")]
        string alias
    )
    {
        var statusLines = new List<string> { };
        await DeferAsync(ephemeral: true);

        if (Context.User is not SocketGuildUser guildUser)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "This command must be used in a server (guild).");
            return;
        }

        if (guildUser.HasAdminPermissions())
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "You do not have permission to manage aliases.");
            return;
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "`alias` is required.");
            return;
        }

        try
        {
            bool removed = await _store.RemoveAsync(alias);
            if (removed)
            {
                _logger.LogInformation("Alias removed by {Actor}: {Alias}", guildUser.Username, alias);
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, $"Removed alias `{alias}`.");
                return;
            }
            else
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, $"Alias `{alias}` not found.");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove alias {Alias}", alias);
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "Failed to remove alias due to an internal error.");
        }
    }

    // ----- LIST SUBCOMMAND -----
    [SlashCommand("list", "List configured aliases")]
    public async Task ListAsync()
    {
        var statusLines = new List<string> { };
        await DeferAsync(ephemeral: true);

        if (Context.User is not SocketGuildUser guildUser)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "This command must be used in a server (guild).");
            return;
        }

        if (guildUser.HasAdminPermissions())
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "You do not have permission to view aliases.");
            return;
        }

        var entries = _store.GetAllAsync()
            .Result.Select(e => $"{e.AliasUserName} -> {e.Author.Ao3ProfileName}")
            .ToList();

        if (entries.Count == 0)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "No aliases configured.");
            return;
        }

        const int maxChunkSize = 1900;
        var sb = new StringBuilder();
        foreach (string? line in entries)
        {
            if (sb.Length + line.Length + 1 > maxChunkSize)
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
}
