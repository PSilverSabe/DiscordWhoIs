using DiscordWhoIs.Core.Extensions;
using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Worker.Services;
using DiscordWhoIs.Worker.Commands.Registry;
using DiscordWhoIs.Worker; // bring Worker class into scope
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Register Core services (DbContextFactory + repositories)
        services.AddDiscordWhoIsCore(hostContext.Configuration);

        // Bind Discord configuration section and register instance
        var discordConfig = hostContext.Configuration.GetSection("Discord").Get<DiscordConfiguration>() ?? new DiscordConfiguration();
        services.AddSingleton(discordConfig);

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