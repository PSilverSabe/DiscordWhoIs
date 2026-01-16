using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscordWhoIs.Core.Databases.DbContexts;

public class DesignTimeBotDbContextFactory : IDesignTimeDbContextFactory<BotDbContext>
{
    public BotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BotDbContext>();

        // IMPORTANT: This connection string is ONLY used during design-time.
        optionsBuilder.UseSqlite(
            "Data Source=design-time.sqlite",
            x => x.MigrationsAssembly(typeof(BotDbContext).Assembly.FullName)
        );

        return new BotDbContext(optionsBuilder.Options);
    }
}
