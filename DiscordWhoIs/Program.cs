using Discord;
using Discord.WebSocket;
using DiscordWhoIs.Cache;
using DiscordWhoIs.Commands;
using DiscordWhoIs.Commands.Handlers;
using DiscordWhoIs.Interfaces;
using DiscordWhoIs.Services;
using DiscordWhoIs.Store;

public class Program
{
    public static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                if (context.HostingEnvironment.IsDevelopment())
                {
                    config.AddUserSecrets<Program>();
                }
                else
                {
                    var fandomTarget = Environment.GetEnvironmentVariable("Fandom_TargetFandom");
                    var discordToken = Environment.GetEnvironmentVariable("Discord_Token");
                    var discordGuildId = Environment.GetEnvironmentVariable("Discord_GuildId");
                    var discordAllowRoleIds = Environment.GetEnvironmentVariable("Discord_AllowRoleId");

                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Fandom:TargetFandom", fandomTarget },
                        { "Discord:Token", discordToken },
                        { "Discord:GuildId", discordGuildId },
                        { "Discord:AllowRoleIds", discordAllowRoleIds }
                    });
                }

                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder => builder.AddConsole());

                services.AddMemoryCache();

                // Register Ao3 typed HttpClient
                services.AddHttpClient<Ao3FicFeedService>();

                services.AddSingleton(sp => new DiscordSocketClient(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged
                }));

                // Persistent SQLite cache (same instance used as IHostedService)
                services.AddSingleton<SqlitePersistentCache>();
                services.AddSingleton<IPersistentCache>(sp => sp.GetRequiredService<SqlitePersistentCache>());
                services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SqlitePersistentCache>());

                // Alias store (separate SQLite file)
                services.AddSingleton<SqliteAliasStore>();
                services.AddSingleton<IAliasStore>(sp => sp.GetRequiredService<SqliteAliasStore>());

                // Register slash command implementations
                services.AddSingleton<ISlashCommand, WhoIsCommand>();
                services.AddSingleton<ISlashCommand, AliasCommand>(); // single command with subcommands

                // Handler that depends on all registered ISlashCommand implementations
                services.AddSingleton<SlashCommandHandler>();

                // Ao3 service (typed client will be created by IHttpClientFactory)
                services.AddSingleton<Ao3FicFeedService>();

                // Bot service
                services.AddSingleton<BotService>();
            })
            .Build();

        await host.Services.GetRequiredService<BotService>().StartAsync();
        await host.RunAsync();
    }
}