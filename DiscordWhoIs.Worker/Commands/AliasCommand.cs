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
            statusLines.Add("This command must be used in a server (guild).");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        bool isAdmin = guildUser.GuildPermissions.Administrator
                      || guildUser.GuildPermissions.ManageGuild
                      || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

        if (!isAdmin)
        {

            statusLines.Add("You do not have permission to manage aliases.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(user))
        {
            statusLines.Add("Both `alias` and `user` are required.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        await _store.AddOrUpdateAsync(alias, user);

        statusLines.Add($"Added/updated alias ``{alias}`` -> ``{user}``");
        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
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
            statusLines.Add("This command must be used in a server (guild).");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        bool isAdmin = guildUser.GuildPermissions.Administrator
                      || guildUser.GuildPermissions.ManageGuild
                      || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

        if (!isAdmin)
        {
            statusLines.Add("You do not have permission to manage aliases.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            statusLines.Add("`alias` is required.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        try
        {
            bool removed = await _store.RemoveAsync(alias);
            if (removed)
            {
                _logger.LogInformation("Alias removed by {Actor}: {Alias}", guildUser.Username, alias);
                statusLines.Add($"Removed alias `{alias}`.");
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
                return;
            }
            else
            {
                statusLines.Add($"Alias `{alias}` not found.");
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove alias {Alias}", alias);
            statusLines.Add("Failed to remove alias due to an internal error.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
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
            statusLines.Add("This command must be used in a server (guild).");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        bool isAdmin = guildUser.GuildPermissions.Administrator
                      || guildUser.GuildPermissions.ManageGuild
                      || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

        if (!isAdmin)
        {
            statusLines.Add("You do not have permission to view aliases.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        var entries = _store.GetAllAsync()
            .Result.Select(e => $"{e.AliasUserName} -> {e.Author.Ao3ProfileName}")
            .ToList();

        if (entries.Count == 0)
        {
            statusLines.Add("No aliases configured.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
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
