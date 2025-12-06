using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.Interfaces;
using System.Text;

namespace DiscordWhoIs.Worker.Commands
{
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
            [Summary("alias", "Alias name")] string alias,
            [Summary("user", "Ao3 account name")] string user)
        {
            await DeferAsync(ephemeral: true);

            if (Context.User is not SocketGuildUser guildUser)
            {
                await FollowupAsync("This command must be used in a server (guild).", ephemeral: true);
                return;
            }

            var isAdmin = guildUser.GuildPermissions.Administrator
                          || guildUser.GuildPermissions.ManageGuild
                          || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

            if (!isAdmin)
            {
                await FollowupAsync("You do not have permission to manage aliases.", ephemeral: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(user))
            {
                await FollowupAsync("Both `alias` and `user` are required.", ephemeral: true);
                return;
            }

            await _store.AddOrUpdateAsync(alias, user);

            await FollowupAsync($"Added/updated alias ``{alias}`` -> ``{user}``");
            return;
        }

        // ----- REMOVE SUBCOMMAND -----
        [SlashCommand("remove", "Remove an alias")]
        public async Task RemoveAsync([Summary("alias", "Alias name to remove")] string alias)
        {
            await DeferAsync(ephemeral: true);

            if (Context.User is not SocketGuildUser guildUser)
            {
                await FollowupAsync("This command must be used in a server (guild).", ephemeral: true);
                return;
            }

            var isAdmin = guildUser.GuildPermissions.Administrator
                          || guildUser.GuildPermissions.ManageGuild
                          || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

            if (!isAdmin)
            {
                await FollowupAsync("You do not have permission to manage aliases.", ephemeral: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(alias))
            {
                await FollowupAsync("`alias` is required.", ephemeral: true);
                return;
            }

            try
            {
                var removed = await _store.RemoveAsync(alias);
                if (removed)
                {
                    _logger.LogInformation("Alias removed by {Actor}: {Alias}", guildUser.Username, alias);
                    await FollowupAsync($"Removed alias `{alias}`.", ephemeral: true);
                    return;
                }
                else
                {
                    await FollowupAsync($"Alias `{alias}` not found.", ephemeral: true);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove alias {Alias}", alias);
                await FollowupAsync("Failed to remove alias due to an internal error.", ephemeral: true);
            }
        }

        // ----- LIST SUBCOMMAND -----
        [SlashCommand("list", "List configured aliases")]
        public async Task ListAsync()
        {
            await DeferAsync(ephemeral: true);

            if (Context.User is not SocketGuildUser guildUser)
            {
                await FollowupAsync("This command must be used in a server (guild).", ephemeral: true);
                return;
            }

            var isAdmin = guildUser.GuildPermissions.Administrator
                          || guildUser.GuildPermissions.ManageGuild
                          || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

            if (!isAdmin)
            {
                await FollowupAsync("You do not have permission to view aliases.", ephemeral: true);
                return;
            }

            var entries = _store.GetAllAsync()
                .Result.Select(e => $"{e.AliasUserName} -> {e.RealUserName}")
                .ToList();

            if (entries.Count == 0)
            {
                await FollowupAsync("No aliases configured.", ephemeral: true);
                return;
            }

            const int maxChunkSize = 1900;
            var sb = new StringBuilder();
            foreach (var line in entries)
            {
                if (sb.Length + line.Length + 1 > maxChunkSize)
                {
                    await FollowupAsync($"```\n{sb}\n```", ephemeral: true);
                    sb.Clear();
                }

                if (sb.Length > 0) sb.AppendLine();
                sb.Append(line);
            }

            if (sb.Length > 0)
            {
                await FollowupAsync($"```\n{sb}\n```", ephemeral: true);
            }

            _logger.LogInformation("Aliases listed by {Actor}", guildUser.Username);
        }
    }
}
