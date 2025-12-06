using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Commands.Registry;
using DiscordWhoIs.Configuration;
using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Databases.Repositories;
using DiscordWhoIs.Services;
using Microsoft.EntityFrameworkCore;
using DiscordWhoIs.Databases.Serializers;
using System.Text.Json.Serialization.Metadata;
using DiscordWhoIs.Controllers.Models;

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
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                })
                .ConfigureWebHostDefaults(web =>
                {
                    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

                    web.UseKestrel()
                       .UseUrls($"http://0.0.0.0:{port}")
                       .Configure((context, app) =>
                       {
                           var uploadConfig = context.Configuration.BindValidated<UploadConfiguration>("Upload");

                           // Authentication Middleware
                           app.Use(async (http, next) =>
                           {
                               if (http.Request.Path.StartsWithSegments("/api"))
                               {
                                   if (!http.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) ||
                                       apiKey != uploadConfig.ApiKey)
                                   {
                                       http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                       await http.Response.WriteAsync("Unauthorized");
                                       return;
                                   }
                               }

                               await next();
                           });

                           var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();

                           if (env.IsDevelopment())
                               app.UseDeveloperExceptionPage();

                           app.UseRouting();
                           app.UseEndpoints(endpoints =>
                           {
                               endpoints.MapGet("/ping", () => Results.Ok(new PingResponse("pong")));
                               endpoints.MapControllers();
                           });
                        });
                })
                .ConfigureServices((context, services) =>
                {
                    // Bind Configurations and Validate
                    var fandomConfig = context.Configuration.BindValidated<FandomConfiguration>("Fandom");
                    var discordConfig = context.Configuration.BindValidated<DiscordConfiguration>("Discord");
                    var botDbContextConfig = context.Configuration.BindValidated<FileLocationConfiguration>("BotDbContext");
                    var uploadConfig = context.Configuration.BindValidated<UploadConfiguration>("Upload");

                    services.AddLogging(b => b.AddConsole());
                    services.AddMemoryCache();

                    // Environment-based database paths
                    var baseDir = AppContext.BaseDirectory;

                    var botDbContext = context.HostingEnvironment.IsDevelopment()
                        ? Path.Combine(baseDir, "botdbcontext.sqlite")
                        : Path.Combine(botDbContextConfig.TargetDirectory, botDbContextConfig.FileName) ?? Path.Combine(baseDir, "botdbcontext.sqlite");

                    // DbContext factories
                    services.AddDbContextFactory<BotDbContext>(options =>
                        options.UseSqlite($"Data Source={botDbContext}"));

                    // Discord Client
                    services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged
                    }));

                    // Caches & Stores
                    services.AddSingleton<AliasRepository>();
                    services.AddSingleton<IAliasRepository>(sp => sp.GetRequiredService<AliasRepository>());

                    services.AddSingleton<FanficRepository>();
                    services.AddSingleton<IFanficRepository>(sp => sp.GetRequiredService<FanficRepository>());

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

                    // Configure JSON serialization so source-generated contexts are used where available
                    // but primitives and other types still fall back to the default resolver.
                    var compositeResolver = new CompositeJsonTypeInfoResolver(
                        ConfigurationJsonContext.Default,
                        new DefaultJsonTypeInfoResolver());

                    // Controllers (MVC) use the composite resolver
                    services.AddControllers()
                            .AddJsonOptions(opts =>
                            {
                                opts.JsonSerializerOptions.TypeInfoResolver = compositeResolver;
                            });

                    // Minimal APIs / Results serialization also needs the resolver
                    services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
                    {
                        opts.SerializerOptions.TypeInfoResolver = compositeResolver;
                    });

                    // Config Bindings
                    services.AddSingleton(fandomConfig);
                    services.AddSingleton(discordConfig);
                    services.AddSingleton(botDbContextConfig);
                    services.AddSingleton(uploadConfig);
                });

            var host = builder.Build();

            // Start Discord bot
            await host.Services.GetRequiredService<BotService>().StartAsync();

            // Start the host (background services, e.g., cache)
            await host.RunAsync();
        }
    }
}