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
        var builder = Host.CreateDefaultBuilder(args)

            // Minimal web server for Render
            .ConfigureWebHostDefaults(webBuilder =>
            {
                // Bind to Render's dynamic PORT
                var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
                webBuilder.UseUrls($"http://0.0.0.0:{port}");

                webBuilder.Configure(app =>
                {
                    // Used because we don't have WebApplicationBuilder here
                    app.UseRouting();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/", async ctx =>
                        {
                            await ctx.Response.WriteAsync("DiscordWhoIs bot is running");
                        });
                    });
                });
            })

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
                        { "Discord:GuildId", Environment.GetEnvironmentVariable("Discord_GuildId") },
                        { "Discord:AllowRoleIds", Environment.GetEnvironmentVariable("Discord_AllowRoleIds") }
                    });
                }
            })

            .ConfigureServices((context, services) =>
            {
                services.AddLogging(b => b.AddConsole());

                services.AddMemoryCache();

                services.AddHttpClient<Ao3FicFeedService>();

                services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged
                }));

                services.AddSingleton<SqlitePersistentCache>();
                services.AddSingleton<IPersistentCache>(sp => sp.GetRequiredService<SqlitePersistentCache>());
                services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SqlitePersistentCache>());

                services.AddSingleton<SqliteAliasStore>();
                services.AddSingleton<IAliasStore>(sp => sp.GetRequiredService<SqliteAliasStore>());

                services.AddSingleton<ISlashCommand, WhoIsCommand>();
                services.AddSingleton<ISlashCommand, AliasCommand>();
                services.AddSingleton<SlashCommandHandler>();

                services.AddSingleton<BotService>();
            });

        var host = builder.Build();

        // Start the Discord bot
        await host.Services.GetRequiredService<BotService>().StartAsync();

        // Start ASP.NET listener for Render health checks
        await host.RunAsync();
    }
}
