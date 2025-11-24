namespace DiscordWhoIs.Commands
{
    using Discord;
    using Discord.WebSocket;
    using DiscordWhoIs.Interfaces;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class AliasCommand : ISlashCommand
    {
        private readonly IAliasStore _store;
        private readonly ILogger<AliasCommand> _logger;

        public AliasCommand(IAliasStore store, ILogger<AliasCommand> logger)
        {
            _store = store;
            _logger = logger;
        }

        public string Name => "alias";

        public ApplicationCommandProperties Build()
        {
            return new SlashCommandBuilder()
                .WithName(Name)
                .WithDescription("Manage AO3 aliases")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("add")
                    .WithDescription("Add or update an alias")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("alias", ApplicationCommandOptionType.String, "Alias name", isRequired: true)
                    .AddOption("user", ApplicationCommandOptionType.String, "AO3 account name", isRequired: true)
                    .AddOption("description", ApplicationCommandOptionType.String, "Optional description for the alias", isRequired: false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("remove")
                    .WithDescription("Remove an alias")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("alias", ApplicationCommandOptionType.String, "Alias name to remove", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("list")
                    .WithDescription("List configured aliases")
                    .WithType(ApplicationCommandOptionType.SubCommand))
                .Build();
        }

        public async Task ExecuteAsync(SocketSlashCommand command)
        {
            var sub = command.Data.Options.FirstOrDefault();
            if (sub == null)
            {
                await command.RespondAsync("No subcommand specified.", ephemeral: true);
                return;
            }

            // All alias management actions require guild context and admin/manage-guild permissions.
            if (!(command.User is SocketGuildUser guildUser))
            {
                await command.RespondAsync("This command must be used in a server (guild).", ephemeral: true);
                return;
            }

            var isAdmin = guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageGuild || guildUser.Roles.Any(x => x.Id == 1358267994303889589);
            var subName = sub.Name;

            switch (subName)
            {
                case "add":
                    if (!isAdmin)
                    {
                        await command.RespondAsync("You do not have permission to manage aliases (Administrator or Manage Guild required).", ephemeral: true);
                        return;
                    }

                    var alias = sub.Options.FirstOrDefault(o => o.Name == "alias")?.Value?.ToString()?.Trim();
                    var user = sub.Options.FirstOrDefault(o => o.Name == "user")?.Value?.ToString()?.Trim();
                    var description = sub.Options.FirstOrDefault(o => o.Name == "description")?.Value?.ToString()?.Trim();

                    if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(user))
                    {
                        await command.RespondAsync("Both `alias` and `user` are required.", ephemeral: true);
                        return;
                    }

                    try
                    {
                        _store.AddOrUpdate(alias!, user!, description);
                        _logger.LogInformation("Alias added/updated by {Actor}: {Alias} -> {User} (desc: {Desc})", guildUser.Username, alias, user, description ?? "<none>");
                        await command.RespondAsync(
                            $"Added/updated alias `{alias}` -> `{user}`{(string.IsNullOrWhiteSpace(description) ? "" : $" with description: `{description}`")}.",
                            ephemeral: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to add/update alias {Alias}", alias);
                        await command.RespondAsync("Failed to add alias due to an internal error.", ephemeral: true);
                    }
                    break;

                case "remove":
                    if (!isAdmin)
                    {
                        await command.RespondAsync("You do not have permission to manage aliases (Administrator or Manage Guild required).", ephemeral: true);
                        return;
                    }

                    var aliasToRemove = sub.Options.FirstOrDefault(o => o.Name == "alias")?.Value?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(aliasToRemove))
                    {
                        await command.RespondAsync("`alias` is required.", ephemeral: true);
                        return;
                    }

                    try
                    {
                        var removed = _store.Remove(aliasToRemove!);
                        if (removed)
                        {
                            _logger.LogInformation("Alias removed by {Actor}: {Alias}", guildUser.Username, aliasToRemove);
                            await command.RespondAsync($"Removed alias `{aliasToRemove}`.", ephemeral: true);
                        }
                        else
                        {
                            await command.RespondAsync($"Alias `{aliasToRemove}` not found.", ephemeral: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to remove alias {Alias}", aliasToRemove);
                        await command.RespondAsync("Failed to remove alias due to an internal error.", ephemeral: true);
                    }
                    break;

                case "list":
                    if (!isAdmin)
                    {
                        await command.RespondAsync("You do not have permission to view aliases (Administrator or Manage Guild required).", ephemeral: true);
                        return;
                    }

                    await command.DeferAsync(ephemeral: true);

                    var entries = _store.GetAllAliases()
                        .OrderBy(e => e.Alias)
                        .Select(e => string.IsNullOrWhiteSpace(e.Description)
                            ? $"{e.Alias} -> {e.Real}"
                            : $"{e.Alias} -> {e.Real} ({e.Description})")
                        .ToList();

                    if (!entries.Any())
                    {
                        await command.FollowupAsync("No aliases configured.", ephemeral: true);
                        return;
                    }

                    const int maxChunkSize = 1900;
                    var sb = new StringBuilder();
                    foreach (var line in entries)
                    {
                        if (sb.Length + line.Length + 1 > maxChunkSize)
                        {
                            await command.FollowupAsync($"```\n{sb}\n```", ephemeral: true);
                            sb.Clear();
                        }

                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(line);
                    }

                    if (sb.Length > 0)
                    {
                        await command.FollowupAsync($"```\n{sb}\n```", ephemeral: true);
                    }

                    _logger.LogInformation("Aliases listed by {Actor}", guildUser.Username);
                    break;

                default:
                    await command.RespondAsync("Unknown subcommand.", ephemeral: true);
                    break;
            }
        }
    }
}
