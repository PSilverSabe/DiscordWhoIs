using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Databases;
using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Logging.Handler;
using DiscordWhoIs.Registry;
using DiscordWhoIs.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddJsonFile("appsettings.json", optional: false);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);

                if (!context.HostingEnvironment.IsDevelopment())
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Fandom:TargetFandom", Environment.GetEnvironmentVariable("Fandom_TargetFandom") },
                        { "Discord:Token", Environment.GetEnvironmentVariable("Discord_Token") },
                        { "Discord:AllowRoleIds", Environment.GetEnvironmentVariable("Discord_AllowRoleIds") },
                        { "Discord:DevMode", Environment.GetEnvironmentVariable("Discord_DevMode") },
                        { "Discord:DevGuildId", Environment.GetEnvironmentVariable("Discord_DevGuildId") },
                        { "Alias:Path", Environment.GetEnvironmentVariable("Alias_Path") },
                        { "Cache:Path", Environment.GetEnvironmentVariable("Cache_Path") },
                        { "Cache:FlushIntervalSeconds", Environment.GetEnvironmentVariable("Cache_FlushIntervalSeconds") },
                        { "Cache:CleanupIntervalSeconds", Environment.GetEnvironmentVariable("Cache_CleanupIntervalSeconds") }
                    });
                }
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                services.AddLogging(b => b.AddConsole());
                services.AddMemoryCache();

                // Persistent HTTP client (fixes Docker Linux hanging issue)
                services.AddHttpClient("Ao3")
                    .ConfigureHttpClient(client =>
                    {
                        client.Timeout = Timeout.InfiniteTimeSpan;
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordWhoIsBot/1.0");
                        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
                    })
                    .ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        return new SocketsHttpHandler
                        {
                            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                            AutomaticDecompression = DecompressionMethods.All,
                            ConnectTimeout = TimeSpan.FromSeconds(10)
                        };
                    })
                    .AddHttpMessageHandler(() => new LoggingHandler());

                services.AddSingleton<Ao3FicFeedService>();

                // Discord Client
                services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged
                }));

                // DB Paths
                var cacheDbFile = configuration["Cache:Path"]?.Trim() ?? Path.Combine(AppContext.BaseDirectory, "persistent_cache.sqlite");
                var aliasDbFile = configuration["Alias:Path"]?.Trim() ?? Path.Combine(AppContext.BaseDirectory, "aliases.sqlite");

                // DbContext factories
                services.AddDbContextFactory<CacheDbContext>(options =>
                    options.UseSqlite($"Data Source={cacheDbFile}"));

                services.AddDbContextFactory<AliasDbContext>(options =>
                    options.UseSqlite($"Data Source={aliasDbFile}"));

                // Caches & Stores
                services.AddSingleton<SqlitePersistentCache>();
                services.AddSingleton<IPersistentCache>(sp => sp.GetRequiredService<SqlitePersistentCache>());
                services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SqlitePersistentCache>());

                services.AddSingleton<SqliteAliasStore>();
                services.AddSingleton<IAliasStore>(sp => sp.GetRequiredService<SqliteAliasStore>());

                // InteractionService
                services.AddSingleton(sp =>
                {
                    var client = sp.GetRequiredService<DiscordSocketClient>();
                    return new InteractionService(client.Rest);
                });

                // CommandRegistry
                services.AddSingleton<CommandRegistry>();

                // Bot Service
                services.AddSingleton<BotService>();
            });

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", false);
        AppContext.SetSwitch("System.Net.Http.EnableActivityPropagation", true);

        var host = builder.Build();

        // Start Discord bot
        await host.Services.GetRequiredService<BotService>().StartAsync();

        // Start the host (background services, e.g., cache)
        await host.RunAsync();
    }
}
