using System.Data.Common;
using System.Text.Json;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Extensions;
using DiscordWhoIs.Core.Filters;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

// ------------------------------------------------------------
// Configure file logging BEFORE building the app
// ------------------------------------------------------------
string logRoot = Path.Combine("databases", "logging");
Directory.CreateDirectory(logRoot);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json",
                optional: true)
            .Build())
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: Path.Combine(logRoot, "web-app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .WriteTo.File(
        path: Path.Combine(logRoot, "web-errors-.log"),
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .CreateLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog(); // Replace default ASP.NET logging

    // ------------------------------------------------------------
    // Shared appsettings.json resolution
    // ------------------------------------------------------------

    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? found = null;

        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "appsettings.json");
            if (File.Exists(candidate))
            {
                found = candidate;
                break;
            }
            dir = dir.Parent;
        }

        if (!string.IsNullOrWhiteSpace(found))
        {
            builder.Configuration.AddJsonFile(found, optional: false, reloadOnChange: true);
        }
    }

    // ------------------------------------------------------------
    // URL binding
    // ------------------------------------------------------------

    string? configuredUrls = builder.Configuration.GetValue<string>("WebHost:Urls")
                         ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

    if (!string.IsNullOrWhiteSpace(configuredUrls))
    {
        builder.WebHost.UseUrls(configuredUrls);
    }

    // ------------------------------------------------------------
    // Services
    // ------------------------------------------------------------

    builder.Services.AddDiscordWhoIsCore(builder.Configuration);
    builder.Services.AddScoped<ApiKeyFilter>();
    builder.Services.AddHealthChecks();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    WebApplication app = builder.Build();

    // ------------------------------------------------------------
    // Database migration & validation
    // ------------------------------------------------------------

    using (IServiceScope scope = app.Services.CreateScope())
    {
        IDbContextFactory<BotDbContext> factory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<BotDbContext>>();

        using BotDbContext context = factory.CreateDbContext();

        Log.Information("Applying database migrations (Web)");

        context.Database.Migrate();

        IEnumerable<string> pending = context.Database.GetPendingMigrations();
        if (pending.Any())
        {
            throw new InvalidOperationException(
                $"Pending migrations detected: {string.Join(", ", pending)}");
        }

        DbConnection connection = context.Database.GetDbConnection();
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
            cmd.ExecuteNonQuery();

            Log.Information("SQLite WAL checkpoint completed (Web)");
        }
        finally
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                connection.Close();
            }
        }
    }

    // ------------------------------------------------------------
    // Middleware
    // ------------------------------------------------------------

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthorization();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration
                })
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    });

    app.MapControllers();

    Log.Information("Web host starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Web host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
