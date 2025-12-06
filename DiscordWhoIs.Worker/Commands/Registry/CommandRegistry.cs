using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordWhoIs.Worker.Commands.Registry
{
    public class CommandRegistry
    {
        private readonly InteractionService _interactions;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<CommandRegistry> _logger;

        public CommandRegistry(
            InteractionService interactions,
            DiscordSocketClient client,
            ILogger<CommandRegistry> logger)
        {
            _interactions = interactions;
            _client = client;
            _logger = logger;
        }

        /// <summary>
        /// Registers global slash commands and logs diff.
        /// </summary>
        public async Task RegisterGlobalAsync()
        {
            var remote = await _client.Rest.GetGlobalApplicationCommands();
            var local = _interactions.SlashCommands.Select(x => x.Name).ToList();

            LogDiff("GLOBAL", remote.Select(r => r.Name).ToList(), local);

            await _interactions.RegisterCommandsGloballyAsync();
        }

        /// <summary>
        /// Registers slash commands to a guild and logs diff.
        /// </summary>
        public async Task RegisterGuildAsync(ulong guildId)
        {
            var remote = await _client.Rest.GetGuildApplicationCommands(guildId);
            var local = _interactions.SlashCommands.Select(x => x.Name).ToList();

            LogDiff($"GUILD {guildId}", remote.Select(r => r.Name).ToList(), local);

            await _interactions.RegisterCommandsToGuildAsync(guildId);
        }

        private void LogDiff(string scope, IList<string> remote, IList<string> local)
        {
            var remoteSet = remote.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var localSet = local.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var added = localSet.Except(remoteSet).ToList();
            var removed = remoteSet.Except(localSet).ToList();
            var unchanged = localSet.Intersect(remoteSet).ToList();

            _logger.LogInformation("======== Slash Command Sync ({Scope}) ========", scope);

            if (added.Any())
                _logger.LogInformation("ADDED: {List}", string.Join(", ", added));

            if (removed.Any())
                _logger.LogInformation("REMOVED: {List}", string.Join(", ", removed));

            if (unchanged.Any())
                _logger.LogInformation("UNCHANGED: {List}", string.Join(", ", unchanged));

            _logger.LogInformation("==============================================");
        }
    }
}
