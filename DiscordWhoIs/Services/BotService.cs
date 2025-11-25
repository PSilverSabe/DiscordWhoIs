using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Registry;
using System;
using System.Reflection;

namespace DiscordWhoIs.Services
{
    public class BotService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly CommandRegistry _registry;
        private readonly IConfiguration _config;
        private readonly ILogger<BotService> _logger;
        private readonly IServiceProvider _services;

        public BotService(
            DiscordSocketClient client,
            InteractionService interactions,
            CommandRegistry registry,
            IConfiguration config,
            IServiceProvider services,
            ILogger<BotService> logger)
        {
            _client = client;
            _interactions = interactions;
            _registry = registry;
            _config = config;
            _services = services;
            _logger = logger;

            _client.Log += LogAsync;
            _client.Ready += OnReadyAsync;
            _client.InteractionCreated += HandleInteractionAsync;
            _client.JoinedGuild += OnJoinedGuildAsync; // auto-register commands for new guilds
        }

        public async Task StartAsync()
        {
            var token = _config["Discord:Token"];
            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("Discord token missing in configuration.");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        /// <summary>
        /// Called on bot ready: loads modules and registers commands globally or to dev guild.
        /// </summary>
        private async Task OnReadyAsync()
        {
            _logger.LogInformation("Connected as {User}", _client.CurrentUser.Username);

            // Load all modules
            await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
            _logger.LogInformation("Discovered {Count} slash command modules.", _interactions.SlashCommands.Count);

            // Clean duplicates globally and per guild
            await CleanupDuplicatesAsync();

            // Register global or dev guild commands
            bool devMode = _config.GetValue<bool>("Discord:DevMode");
            ulong devGuildId = _config.GetValue<ulong>("Discord:DevGuildId");

            if (devMode)
            {
                _logger.LogInformation("Dev mode enabled — registering commands to guild {GuildId}", devGuildId);
                await _registry.RegisterGuildAsync(devGuildId);
            }
            else
            {
                _logger.LogInformation("Registering global commands.");
                await _registry.RegisterGlobalAsync();
            }

            _logger.LogInformation("Bot is fully ready.");
        }



        /// <summary>
        /// Handles a new guild join by registering commands immediately.
        /// </summary>
        private async Task OnJoinedGuildAsync(SocketGuild guild)
        {
            try
            {
                _logger.LogInformation("Joined new guild: {GuildName} ({GuildId})", guild.Name, guild.Id);

                // Fetch existing global commands
                var globalCommands = await _client.Rest.GetGlobalApplicationCommands();

                // Determine which local commands are missing globally
                var missingCommands = _interactions.SlashCommands
                    .Where(local => !globalCommands.Any(g => g.Name.Equals(local.Name, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                // Register missing commands to guild if needed
                if (missingCommands.Length > 0)
                {
                    _logger.LogInformation("Adding {Count} commands to guild {GuildId} that are missing globally.", missingCommands.Length, guild.Id);
                    await _interactions.AddCommandsToGuildAsync(guild.Id, deleteMissing: false, commands: missingCommands);
                }
                else
                {
                    _logger.LogInformation("All commands already exist globally. No need to register locally for guild {GuildId}", guild.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle commands for new guild {GuildName} ({GuildId})", guild.Name, guild.Id);
            }
        }

        /// <summary>
        /// Removes duplicate commands from global scope and all guilds.
        /// Keeps only the first instance of each command by name.
        /// </summary>
        public async Task CleanupDuplicatesAsync()
        {
            try
            {
                // -----------------------
                // Clean global commands
                // -----------------------
                var globalCommands = await _client.Rest.GetGlobalApplicationCommands();

                var duplicateGlobals = globalCommands
                    .GroupBy(c => c.Name.ToLowerInvariant())
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.Skip(1))
                    .ToList();

                foreach (var dup in duplicateGlobals)
                {
                    await dup.DeleteAsync();
                    _logger.LogInformation("Deleted duplicate global command '{Command}'", dup.Name);
                }

                if (!duplicateGlobals.Any())
                    _logger.LogInformation("No duplicate global commands found.");

                // -----------------------
                // Clean guild commands
                // -----------------------
                foreach (var guild in _client.Guilds)
                {
                    try
                    {
                        var guildCommands = await _client.Rest.GetGuildApplicationCommands(guild.Id);

                        var duplicateGuilds = guildCommands
                            .GroupBy(c => c.Name.ToLowerInvariant())
                            .Where(g => g.Count() > 1)
                            .SelectMany(g => g.Skip(1))
                            .ToList();

                        foreach (var dup in duplicateGuilds)
                        {
                            await dup.DeleteAsync();
                            _logger.LogInformation("Deleted duplicate command '{Command}' in guild {GuildId}", dup.Name, guild.Id);
                        }

                        if (!duplicateGuilds.Any())
                            _logger.LogInformation("No duplicate commands found in guild {GuildId}", guild.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to clean duplicates in guild {GuildName} ({GuildId})", guild.Name, guild.Id);
                    }
                }

                _logger.LogInformation("Duplicate command cleanup complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean duplicate global commands.");
            }
        }

        /// <summary>
        /// Executes interactions through InteractionService.
        /// </summary>
        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            try
            {
                var ctx = new SocketInteractionContext(_client, interaction);
                await _interactions.ExecuteCommandAsync(ctx, _services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing interaction.");
                if (interaction.Type == InteractionType.ApplicationCommand)
                    await interaction.RespondAsync("An error occurred while executing this command.", ephemeral: true);
            }
        }

        /// <summary>
        /// Logs Discord client messages to console/logs.
        /// </summary>
        private Task LogAsync(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    _logger.LogError(msg.ToString());
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning(msg.ToString());
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation(msg.ToString());
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    _logger.LogDebug(msg.ToString());
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
