using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Cache;
using DiscordWhoIs.Interfaces;
using DiscordWhoIs.Registry;
using DiscordWhoIs.Services;
using DiscordWhoIs.Store;
using System.Diagnostics.CodeAnalysis;

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
                    // Override with environment variables in production
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Fandom:TargetFandom", Environment.GetEnvironmentVariable("Fandom_TargetFandom") },
                        { "Discord:Token", Environment.GetEnvironmentVariable("Discord_Token") },
                        { "Discord:AllowRoleIds", Environment.GetEnvironmentVariable("Discord_AllowRoleIds") },
                        { "Discord:DevMode", Environment.GetEnvironmentVariable("Discord_DevMode") },
                        { "Discord:DevGuildId", Environment.GetEnvironmentVariable("Discord_DevGuildId") }
                    });
                }
            })

            .ConfigureServices((context, services) =>
            {
                services.AddLogging(b => b.AddConsole());
                services.AddMemoryCache();

                services.AddHttpClient<Ao3FicFeedService>();

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
                services.AddSingleton<InteractionService>(sp =>
                {
                    var client = sp.GetRequiredService<DiscordSocketClient>();
                    return new InteractionService(client.Rest);
                });

                // ******** NEW: CommandRegistry ********
                services.AddSingleton<CommandRegistry>();

                // Bot Service
                services.AddSingleton<BotService>();
            });

        var host = builder.Build();

        // Start Discord bot
        await host.Services.GetRequiredService<BotService>().StartAsync();

        // Start ASP.NET listener
        await host.RunAsync();
    }
}
