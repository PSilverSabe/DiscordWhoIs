using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Commands.Registry;
using DiscordWhoIs.Configuration;
using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases;
using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Logging.Handler;
using DiscordWhoIs.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace DiscordWhoIs
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddEnvironmentVariables();
                    config.AddEnvironmentVariables(prefix: "DW_");
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // Bind Configurations and Validate
                    var fandomConfig = context.Configuration.BindValidated<FandomConfiguration>("Fandom");
                    var discordConfig = context.Configuration.BindValidated<DiscordConfiguration>("Discord");
                    var ao3Config = context.Configuration.BindValidated<Ao3Configuration>("Ao3");
                    var aliasConfig = context.Configuration.BindValidated<AliasConfiguration>("Alias");
                    var cacheConfig = context.Configuration.BindValidated<CacheConfiguration>("Cache");

                    services.AddLogging(b => b.AddConsole());
                    services.AddMemoryCache();

                    // Persistent HTTP client (fixes Docker Linux hanging issue)
                    services.AddHttpClient("Ao3")
                        .ConfigureHttpClient(client =>
                        {
                            client.Timeout = TimeSpan.FromSeconds(30);
                            client.DefaultRequestHeaders.UserAgent.Clear();
                            client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordWhoIsBot/1.0 (+31625469+PSilverSabe@users.noreply.github.com)");
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

                    // Environment-based database paths
                    var baseDir = AppContext.BaseDirectory;

                    var cacheDb = context.HostingEnvironment.IsDevelopment()
                        ? Path.Combine(baseDir, "cache.sqlite")
                        : cacheConfig.Path ?? Path.Combine(baseDir, "cache.sqlite");

                    var aliasDb = context.HostingEnvironment.IsDevelopment()
                        ? Path.Combine(baseDir, "aliases.sqlite")
                        : aliasConfig.Path ?? Path.Combine(baseDir, "aliases.sqlite");

                    // DbContext factories
                    services.AddDbContextFactory<CacheDbContext>(options =>
                        options.UseSqlite($"Data Source={cacheDb}"));

                    services.AddDbContextFactory<AliasDbContext>(options =>
                        options.UseSqlite($"Data Source={aliasDb}"));

                    // Ao3 Fic Feed Service
                    services.AddSingleton<Ao3FicFeedService>();

                    // Discord Client
                    services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged
                    }));

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

                    // Config Bindings
                    services.AddSingleton(fandomConfig);
                    services.AddSingleton(discordConfig);
                    services.AddSingleton(aliasConfig);
                    services.AddSingleton(cacheConfig);
                    services.AddSingleton(ao3Config);
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
}