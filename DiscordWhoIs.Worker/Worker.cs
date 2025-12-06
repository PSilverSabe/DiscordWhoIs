using DiscordWhoIs.Worker.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordWhoIs.Worker
{
    public class Worker : BackgroundService
    {
        private readonly BotService _botService;
        private readonly ILogger<Worker> _logger;

        public Worker(BotService botService, ILogger<Worker> logger)
        {
            _botService = botService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker starting BotService.");
            try
            {
                await _botService.StartAsync();
                // Keep running until cancelled
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker encountered an error.");
            }
            _logger.LogInformation("Worker stopping.");
        }
    }
}
