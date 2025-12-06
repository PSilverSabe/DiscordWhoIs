using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Core.Databases.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using DiscordWhoIs.Core.Configuration;

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

            // Environment-based database paths
            var baseDir = AppContext.BaseDirectory;

            var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isDevelopment = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase);
    
            string botDbContext;
            if (isDevelopment)
            {
                botDbContext = Path.Combine(baseDir, "botdbcontext.sqlite");
            }
            else
            {
                var targetDir = string.IsNullOrWhiteSpace(botDbContextConfig.TargetDirectory) ? baseDir : botDbContextConfig.TargetDirectory;
                var fileName = string.IsNullOrWhiteSpace(botDbContextConfig.FileName) ? "botdbcontext.sqlite" : botDbContextConfig.FileName;
                botDbContext = Path.Combine(targetDir, fileName);
            }

            if (string.IsNullOrWhiteSpace(botDbContext))
            {
                throw new InvalidOperationException("Bot database connection string is not configured.");
            }

            // Register DbContextFactory in Core so both Web and Worker can create contexts safely
            services.AddDbContextFactory<BotDbContext>(options =>
            {
                options.UseSqlite(botDbContext);
            });

            // Repository uses an in-memory concurrent store and IDbContextFactory; keep a singleton so the cache is shared
            services.AddSingleton<IFanficRepository, FanficRepository>();

            // Config Bindings
            services.AddSingleton(fandomConfig);
            services.AddSingleton(discordConfig);
            services.AddSingleton(botDbContextConfig);
            services.AddSingleton(uploadConfig);

            return services;
        }
    }
}