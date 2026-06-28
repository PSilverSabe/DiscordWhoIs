using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Extensions;
using DiscordWhoIs.Worker;
using DiscordWhoIs.Worker.Commands.Modals.Handlers;
using DiscordWhoIs.Worker.Commands.Registry;
using DiscordWhoIs.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// ------------------------------------------------------------
// Configure file logging BEFORE host build
// ------------------------------------------------------------
const string DatabaseRoot = "/databases";
string logRoot = Path.Combine(DatabaseRoot, "logging");
Directory.CreateDirectory(logRoot);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json",
                optional: true)
            .Build())
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: Path.Combine(logRoot, "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .WriteTo.File(
        path: Path.Combine(logRoot, "errors-.log"),
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .CreateLogger();

try
{
    IHost host = Host.CreateDefaultBuilder(args)
        .UseSerilog() // replace default logging pipeline
        .ConfigureAppConfiguration((context, config) =>
        {
            // If a repository-level appsettings.json exists, add it so the worker uses the same settings.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            string? found = null;

            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "appsettings.json");
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
            services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds
                               | GatewayIntents.GuildMessages
                               | GatewayIntents.MessageContent
                               | GatewayIntents.GuildMembers
            }));

            services.AddSingleton<InteractionService>(sp =>
                new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));

            // Add Memory Cache
            services.AddMemoryCache();

            // Command registry and bot service
            services.AddSingleton<AuthorDescriptionModalHandler>();
            services.AddSingleton<CommandRegistry>();
            services.AddSingleton<ActiveUsersCacheService>();
            services.AddSingleton<BotService>();
            services.AddSingleton<FanficEmbedResponderService>();
            services.AddSingleton<ModalRouter>();

            // Background worker wrapper that runs the bot
            services.AddHostedService<Worker>();
        })
        .Build();

    // ------------------------------------------------------------
    // Database migration & validation
    // ------------------------------------------------------------

    using (IServiceScope scope = host.Services.CreateScope())
    {
        IDbContextFactory<BotDbContext> factory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<BotDbContext>>();

        using BotDbContext context = factory.CreateDbContext();

        Log.Information("Applying database migrations");
        context.Database.Migrate();

        IEnumerable<string> pending = context.Database.GetPendingMigrations();
        if (pending.Any())
        {
            throw new InvalidOperationException(
                $"Pending migrations detected: {string.Join(", ", pending)}");
        }

        DbConnection connection = context.Database.GetDbConnection();
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
            cmd.ExecuteNonQuery();

            Log.Information("SQLite WAL checkpoint completed");
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                connection.Close();
            }
        }
    }

    Log.Information("Host starting");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
