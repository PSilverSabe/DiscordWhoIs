using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Extensions;
using DiscordWhoIs.Worker;
using DiscordWhoIs.Worker.Commands.Registry;
using DiscordWhoIs.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
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
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));

        // Command registry and bot service
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<ActiveUsersCacheService>();
        services.AddSingleton<BotService>();

        // Background worker wrapper that runs the bot
        services.AddHostedService<Worker>();
    })
    .Build();

using (IServiceScope scope = host.Services.CreateScope())
{
    IDbContextFactory<BotDbContext> factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BotDbContext>>();

    using BotDbContext context = factory.CreateDbContext();
    context.Database.Migrate();

    IEnumerable<string> pending = context.Database.GetPendingMigrations();
    if (pending.Any())
    {
        throw new InvalidOperationException($"Pending migrations detected: {string.Join(", ", pending)}");
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
    }
    finally
    {
        if (connection.State == System.Data.ConnectionState.Open)
        {
            connection.Close();
        }
    }
}

await host.RunAsync();
