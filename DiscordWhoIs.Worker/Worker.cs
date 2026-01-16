using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordWhoIs.Worker.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker;

public class Worker(BotService botService, ILogger<Worker> logger) : BackgroundService
{
    private readonly BotService _botService = botService;
    private readonly ILogger<Worker> _logger = logger;

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
