using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Cache;
using DiscordWhoIs.Interfaces;
using DiscordWhoIs.Logging.Handler;
using DiscordWhoIs.Registry;
using DiscordWhoIs.Services;
using DiscordWhoIs.Store;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;

public class Program
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DiscordWhoIs.Commands.WhoIsCommandModule))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(DiscordWhoIs.Commands.AliasCommandModule))]
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
                        { "Cache:ExpirationInHours", Environment.GetEnvironmentVariable("Cache_Experiation_In_Hours") }
                    });
                }
            })

            .ConfigureServices((context, services) =>
            {
                services.AddLogging(b => b.AddConsole());
                services.AddMemoryCache();

                // ---------------------------------------------------------
                // Persistent HTTP client (fixes Docker Linux hanging issue)
                // ---------------------------------------------------------
                services.AddHttpClient<Ao3FicFeedService>(client =>
                {
                    client.Timeout = Timeout.InfiniteTimeSpan; // we manage timeout manually
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:98.0) Gecko/20100101 Firefox/98.0");
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                    client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                })
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var handler = new SocketsHttpHandler
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5), // Fixes Docker DNS stalling
                        AutomaticDecompression = DecompressionMethods.All,
                        ConnectTimeout = TimeSpan.FromSeconds(10)
                    };

                    Console.WriteLine("### HttpClient Handler Created ###");
                    Console.WriteLine("DNS refresh interval = 5 minutes");
                    Console.WriteLine("ConnectTimeout       = 10s");
                    Console.WriteLine("Pooled connections   = Enabled");
                    Console.WriteLine("Using SocketsHttpHandler");

                    return handler;
                });

                // Discord Client
                services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged
                }));

                // SQLite Cache + Hosted Service
                services.AddSingleton<SqlitePersistentCache>();
                services.AddSingleton<IPersistentCache>(sp => sp.GetRequiredService<SqlitePersistentCache>());
                services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SqlitePersistentCache>());

                services.AddSingleton<SqliteAliasStore>();
                services.AddSingleton<IAliasStore>(sp => sp.GetRequiredService<SqliteAliasStore>());

                // ******** NEW: InteractionService ********
                services.AddSingleton(sp =>
                {
                    var client = sp.GetRequiredService<DiscordSocketClient>();
                    return new InteractionService(client.Rest);
                });

                // ******** NEW: CommandRegistry ********
                services.AddSingleton<CommandRegistry>();

                // Bot Service
                services.AddSingleton<BotService>();

                services.AddHttpClient<Ao3FicFeedService>().AddHttpMessageHandler(() => new LoggingHandler());
            });
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", false);
        AppContext.SetSwitch("System.Net.Http.EnableActivityPropagation", true);
        var host = builder.Build();

        // Start Discord bot
        await host.Services.GetRequiredService<BotService>().StartAsync();

        // Start ASP.NET listener
        await host.RunAsync();
    }
}
