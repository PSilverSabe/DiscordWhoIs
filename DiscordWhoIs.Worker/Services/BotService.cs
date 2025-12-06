using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Worker.Commands.Registry;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordWhoIs.Worker.Services
{
    public class BotService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly CommandRegistry _registry;
        private readonly ILogger<BotService> _logger;
        private readonly IServiceProvider _services;
        private readonly DiscordConfiguration _discordConfig;

        public BotService(
            DiscordSocketClient client,
            InteractionService interactions,
            CommandRegistry registry,
            IServiceProvider services,
            ILogger<BotService> logger,
            DiscordConfiguration discordOptions)
        {
            _client = client;
            _interactions = interactions;
            _registry = registry;
            _services = services;
            _logger = logger;
            _discordConfig = discordOptions;

            _client.Log += LogAsync;
            _client.Ready += OnReadyAsync;
            _client.InteractionCreated += HandleInteractionAsync;
            _client.JoinedGuild += OnJoinedGuildAsync; // auto-register commands for new guilds
        }

        public async Task StartAsync()
        {
            await _client.LoginAsync(TokenType.Bot, _discordConfig.Token);
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
            if (_discordConfig.DevMode && _discordConfig.DevGuildId.HasValue)
            {
                _logger.LogInformation("Dev mode enabled — registering commands to guild {GuildId}", _discordConfig.DevGuildId);
                _logger.LogInformation("Cleaning up local and global commands before registering to dev guild.");
                // Remove any existing local commands to avoid conflicts
                foreach (var dup in await _client.Rest.GetGuildApplicationCommands(_discordConfig.DevGuildId.Value))
                {
                    await dup.DeleteAsync();
                    _logger.LogInformation("Deleted duplicate local command '{Command}' in guild {GuildId}", dup.Name, _discordConfig.DevGuildId);
                }
                foreach (var dup in await _client.Rest.GetGlobalApplicationCommands())
                {
                    await dup.DeleteAsync();
                    _logger.LogInformation("Deleted duplicate global command '{Command}' in guild {GuildId}", dup.Name, _discordConfig.DevGuildId);
                }

                await _registry.RegisterGuildAsync(_discordConfig.DevGuildId.Value);
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
                // Clean guild commands
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

                if (duplicateGlobals.Count == 0)
                    _logger.LogInformation("No duplicate global commands found.");

                // Clean guild commands
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

                        if (duplicateGuilds.Count == 0)
                        {
                            _logger.LogInformation("No duplicate commands found in guild {GuildId}", guild.Id);
                        }
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
            const string template = "{Message}";

            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    _logger.LogError(template, msg.ToString());
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning(template, msg.ToString());
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation(template, msg.ToString());
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    _logger.LogDebug(template, msg.ToString());
                    break;
            }

            return Task.CompletedTask;
        }

    }
}
