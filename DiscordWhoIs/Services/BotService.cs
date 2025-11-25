using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Commands;
using DiscordWhoIs.Registry;
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
            _logger = logger;
            _services = services;

            _client.Log += LogAsync;
            _client.Ready += OnReadyAsync;
            _client.InteractionCreated += HandleInteractionAsync;
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
        /// Automatically called when the bot is ready.
        /// Loads all modules, then registers commands globally or to dev guild.
        /// </summary>
        private async Task OnReadyAsync()
        {
            _logger.LogInformation("Connected as {User}", _client.CurrentUser.Username);

            // Dynamic module loading, does not work with Trimming or AOT
            await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

            // Manual module loading to support Trimming and AOT
            // await _interactions.AddModuleAsync<WhoIsCommandModule>(_services);
            // await _interactions.AddModuleAsync<AliasCommandModule>(_services);

            _logger.LogInformation("Discovered {Count} slash command modules.", _interactions.SlashCommands.Count);

            // Determine if we are in dev mode (guild-only) or global mode
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
        /// Handles all incoming interactions via InteractionService.
        /// </summary>
        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            try
            {
                var ctx = new SocketInteractionContext(_client, interaction);
                await _interactions.ExecuteCommandAsync(ctx, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing interaction.");
                if (interaction.Type == InteractionType.ApplicationCommand)
                    await interaction.RespondAsync("An error occurred while executing this command.", ephemeral: true);
            }
        }

        /// <summary>
        /// Logs Discord client messages to console/logs
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
