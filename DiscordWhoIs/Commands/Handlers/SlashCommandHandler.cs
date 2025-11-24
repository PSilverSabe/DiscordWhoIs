namespace DiscordWhoIs.Commands.Handlers
{
    using Discord.WebSocket;
    using DiscordWhoIs.Interfaces;
    using Microsoft.Extensions.Configuration;

    public class SlashCommandHandler
    {
        private readonly IEnumerable<ISlashCommand> _commands;
        private readonly ILogger<SlashCommandHandler> _logger;

        public SlashCommandHandler(IEnumerable<ISlashCommand> commands, ILogger<SlashCommandHandler> logger)
        {
            _commands = commands;
            _logger = logger;
        }

        public async Task RegisterCommandsAsync(DiscordSocketClient client, IConfiguration config)
        {
            // If a GUILD_ID is supplied in configuration, register there (fast). Otherwise register globally.
            if (ulong.TryParse(config["Discord:GuildId"], out var guildId) && guildId != 0)
            {
                _logger.LogInformation("Registering commands to guild {GuildId}", guildId);
                var guild = client.GetGuild(guildId);
                if (guild is null)
                {
                    _logger.LogWarning("Guild {GuildId} not available on current client. Falling back to global commands.", guildId);
                }
                else
                {
                    var properties = _commands.Select(c => c.Build()).ToArray();
                    await guild.BulkOverwriteApplicationCommandAsync(properties);
                    return;
                }
            }

            _logger.LogInformation("Registering global commands (may take up to 1 hour to propagate)");
            var globalProps = _commands.Select(c => c.Build()).ToArray();
            await client.BulkOverwriteGlobalApplicationCommandsAsync(globalProps);
        }

        public async Task HandleAsync(SocketSlashCommand command)
        {
            var handler = _commands.FirstOrDefault(c => c.Name == command.Data.Name);
            if (handler != null)
            {
                try
                {
                    await handler.ExecuteAsync(command);
                }
                catch (Exception ex)
                {
                    // Catch errors from handlers to avoid crashing the bot
                    await command.FollowupAsync("An error occurred while executing the command.", ephemeral: true);
                }
            }
            else
            {
                await command.RespondAsync("Unknown command.", ephemeral: true);
            }
        }
    }
}
