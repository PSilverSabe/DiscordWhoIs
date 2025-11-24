namespace DiscordWhoIs.Services
{
    using Discord;
    using Discord.WebSocket;
    using DiscordWhoIs.Commands.Handlers;

    public class BotService
    {
        private readonly DiscordSocketClient _client;
        private readonly IConfiguration _config;
        private readonly SlashCommandHandler _slashHandler;
        private readonly ILogger<BotService> _logger;

        public BotService(DiscordSocketClient client, IConfiguration config, SlashCommandHandler slashHandler, ILogger<BotService> logger)
        {
            _client = client;
            _config = config;
            _slashHandler = slashHandler;
            _logger = logger;

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.SlashCommandExecuted += _slashHandler.HandleAsync;
        }

        public async Task StartAsync()
        {
            var token = _config["Discord:Token"];
            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("Discord token missing in configuration.");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        private async Task ReadyAsync()
        {
            _logger.LogInformation("Connected as {User}", _client.CurrentUser.Username);
            await _slashHandler.RegisterCommandsAsync(_client, _config);
        }

        private Task LogAsync(LogMessage message)
        {
            _logger.LogInformation(message.ToString());
            return Task.CompletedTask;
        }
    }
}
