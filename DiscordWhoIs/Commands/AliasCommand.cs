using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Interfaces;
using System.Text;

namespace DiscordWhoIs.Commands
{
    [Group("alias", "Manage AO3 aliases")]
    public class AliasCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IAliasStore _store;
        private readonly ILogger<AliasCommandModule> _logger;

        public AliasCommandModule(IAliasStore store, ILogger<AliasCommandModule> logger)
        {
            _store = store;
            _logger = logger;
        }

        // ----- ADD SUBCOMMAND -----
        [SlashCommand("add", "Add or update an alias")]
        public async Task AddAsync(
            [Summary("alias", "Alias name")] string alias,
            [Summary("user", "AO3 account name")] string user,
            [Summary("description", "Optional description")] string description = null)
        {
            if (!(Context.User is SocketGuildUser guildUser))
            {
                await RespondAsync("This command must be used in a server (guild).", ephemeral: true);
                return;
            }

            var isAdmin = guildUser.GuildPermissions.Administrator
                          || guildUser.GuildPermissions.ManageGuild
                          || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

            if (!isAdmin)
            {
                await RespondAsync("You do not have permission to manage aliases.", ephemeral: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(user))
            {
                await RespondAsync("Both `alias` and `user` are required.", ephemeral: true);
                return;
            }

            try
            {
                _store.AddOrUpdate(alias, user, description);
                _logger.LogInformation("Alias added/updated by {Actor}: {Alias} -> {User} (desc: {Desc})",
                    guildUser.Username, alias, user, description ?? "<none>");

                await RespondAsync(
                    $"Added/updated alias `{alias}` -> `{user}`" +
                    (string.IsNullOrWhiteSpace(description) ? "" : $" with description: `{description}`"),
                    ephemeral: true);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to add/update alias {Alias}", alias);
                await RespondAsync("Failed to add alias due to an internal error.", ephemeral: true);
            }
        }

        // ----- REMOVE SUBCOMMAND -----
        [SlashCommand("remove", "Remove an alias")]
        public async Task RemoveAsync([Summary("alias", "Alias name to remove")] string alias)
        {
            if (!(Context.User is SocketGuildUser guildUser))
            {
                await RespondAsync("This command must be used in a server (guild).", ephemeral: true);
                return;
            }

            var isAdmin = guildUser.GuildPermissions.Administrator
                          || guildUser.GuildPermissions.ManageGuild
                          || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

            if (!isAdmin)
            {
                await RespondAsync("You do not have permission to manage aliases.", ephemeral: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(alias))
            {
                await RespondAsync("`alias` is required.", ephemeral: true);
                return;
            }

            try
            {
                var removed = _store.Remove(alias);
                if (removed)
                {
                    _logger.LogInformation("Alias removed by {Actor}: {Alias}", guildUser.Username, alias);
                    await RespondAsync($"Removed alias `{alias}`.", ephemeral: true);
                }
                else
                {
                    await RespondAsync($"Alias `{alias}` not found.", ephemeral: true);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to remove alias {Alias}", alias);
                await RespondAsync("Failed to remove alias due to an internal error.", ephemeral: true);
            }
        }

        // ----- LIST SUBCOMMAND -----
        [SlashCommand("list", "List configured aliases")]
        public async Task ListAsync()
        {
            if (!(Context.User is SocketGuildUser guildUser))
            {
                await RespondAsync("This command must be used in a server (guild).", ephemeral: true);
                return;
            }

            var isAdmin = guildUser.GuildPermissions.Administrator
                          || guildUser.GuildPermissions.ManageGuild
                          || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

            if (!isAdmin)
            {
                await RespondAsync("You do not have permission to view aliases.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            var entries = _store.GetAllAliases()
                .OrderBy(e => e.Alias)
                .Select(e => string.IsNullOrWhiteSpace(e.Description)
                    ? $"{e.Alias} -> {e.Real}"
                    : $"{e.Alias} -> {e.Real} ({e.Description})")
                .ToList();

            if (!entries.Any())
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
