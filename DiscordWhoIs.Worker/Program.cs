using DiscordWhoIs.Core.Extensions;
using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Worker.Services;
using DiscordWhoIs.Worker.Commands.Registry;
using DiscordWhoIs.Worker;
using Discord.Interactions;
using Discord.WebSocket;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        // If a repository-level appsettings.json exists, add it so the worker uses the same settings.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? found = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "appsettings.json");
            if (File.Exists(candidate))
            {
                found = candidate;
                break;
            }
            dir = dir.Parent;
        }

        if (!string.IsNullOrWhiteSpace(found))
        {
            config.AddJsonFile(found, optional: false, reloadOnChange: true);
        }

        config.AddUserSecrets<Program>();
    })
    .ConfigureServices((hostContext, services) =>
    {
        // Register Core services (DbContextFactory + repositories)
        services.AddDiscordWhoIsCore(hostContext.Configuration);

        // Discord.NET core services
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));

        // Command registry and bot service
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<BotService>();

        // Background worker wrapper that runs the bot
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();