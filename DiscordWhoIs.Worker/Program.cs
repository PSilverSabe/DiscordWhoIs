using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Commands.Registry;
using DiscordWhoIs.Configuration;
using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Repositories;
using DiscordWhoIs.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
        config.AddJsonFile("appsettings.json", optional: false);
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        // Bind configs
        var discordConfig = context.Configuration.BindValidated<DiscordConfiguration>("Discord");
        var botDbContextConfig = context.Configuration.BindValidated<FileLocationConfiguration>("BotDbContext");

        // DbContext factory
        var baseDir = AppContext.BaseDirectory;
        var botDbContext = context.HostingEnvironment.IsDevelopment()
            ? Path.Combine(baseDir, "botdbcontext.sqlite")
            : Path.Combine(botDbContextConfig.TargetDirectory, botDbContextConfig.FileName) ?? Path.Combine(baseDir, "botdbcontext.sqlite");

        services.AddDbContextFactory<DiscordWhoIs.Databases.DbContexts.BotDbContext>(options =>
            options.UseSqlite($"Data Source={botDbContext}"));

        // Discord client
        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged
        }));

        // Interaction service
        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<DiscordSocketClient>();
            return new InteractionService(client.Rest);
        });

        // Repositories
        services.AddSingleton<DiscordWhoIs.Databases.Repositories.AliasRepository>();
        services.AddSingleton<DiscordWhoIs.Databases.Interfaces.IAliasRepository>(sp => sp.GetRequiredService<DiscordWhoIs.Databases.Repositories.AliasRepository>());

        services.AddSingleton<DiscordWhoIs.Databases.Repositories.FanficRepository>();
        services.AddSingleton<DiscordWhoIs.Databases.Interfaces.IFanficRepository>(sp => sp.GetRequiredService<DiscordWhoIs.Databases.Repositories.FanficRepository>());

        // CommandRegistry and bot service
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<DiscordWhoIs.Worker.Services.BotService>();

        // Configs
        services.AddSingleton(discordConfig);
    });

var host = builder.Build();

// Start BotService
await host.Services.GetRequiredService<DiscordWhoIs.Worker.Services.BotService>().StartAsync();

// Run host
await host.RunAsync();