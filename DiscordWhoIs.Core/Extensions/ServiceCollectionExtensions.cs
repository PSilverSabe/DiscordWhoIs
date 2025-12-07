using DiscordWhoIs.Core.Configuration;
using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Core.Databases.Repositories;
using DiscordWhoIs.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordWhoIs.Core.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDiscordWhoIsCore(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind Configurations and Validate
            var fandomConfig = configuration.BindValidated<FandomConfiguration>("Fandom");
            var discordConfig = configuration.BindValidated<DiscordConfiguration>("Discord");
            var botDbContextConfig = configuration.BindValidated<FileLocationConfiguration>("BotDbContext");
            var uploadConfig = configuration.BindValidated<UploadConfiguration>("Upload");

            // Resolve file locations using unified logic
            string botDbPath = PathResolver.ResolvePath(
                botDbContextConfig.TargetDirectory,
                botDbContextConfig.FileName ?? "botdbcontext.sqlite");

            string uploadFilePath = PathResolver.ResolvePath(
                uploadConfig.TargetDirectory,
                uploadConfig.FileName ?? "fanfic_updates.csv");

            // Register DbContextFactory in Core so both Web and Worker can create contexts safely
            services.AddDbContextFactory<BotDbContext>(options =>
            {
                options.UseSqlite($"Data Source={botDbPath}");
            });

            // Repositories with DB interaction
            services.AddSingleton<IFanficRepository, FanficRepository>();
            services.AddSingleton<IAliasRepository, AliasRepository>();

            // Config Bindings
            services.AddSingleton(fandomConfig);
            services.AddSingleton(discordConfig);
            services.AddSingleton(botDbContextConfig);
            services.AddSingleton(uploadConfig);

            // Store resolved paths for downstream services
            services.AddSingleton(new ResolvedPaths
            {
                BotDbPath = botDbPath,
                UploadFilePath = uploadFilePath
            });

            return services;
        }
    }
}