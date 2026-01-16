using System.Data.Common;
using System.Text.Json;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Extensions;
using DiscordWhoIs.Core.Filters;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// If a repository-level appsettings.json exists, load it so all projects share the same settings.
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
        // ConfigurationManager (builder.Configuration) implements IConfigurationBuilder so AddJsonFile is available
        builder.Configuration.AddJsonFile(found, optional: false, reloadOnChange: true);
    }
}

// Read URLs from configuration ("WebHost:Urls") or environment ("ASPNETCORE_URLS")
string? configuredUrls = builder.Configuration.GetValue<string>("WebHost:Urls")
                     ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!string.IsNullOrWhiteSpace(configuredUrls))
{
    builder.WebHost.UseUrls(configuredUrls);
}

// Register Core services (DbContextFactory + repositories)
builder.Services.AddDiscordWhoIsCore(builder.Configuration);

// Register the ApiKeyFilter so TypeFilter can resolve it
builder.Services.AddScoped<ApiKeyFilter>();

// Health checks
builder.Services.AddHealthChecks();

builder.Services.AddControllers(); // your normal web registrations
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    IDbContextFactory<BotDbContext> factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<BotDbContext>>();

    using BotDbContext context = factory.CreateDbContext();

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
    }
    finally
    {
        if (connection.State == System.Data.ConnectionState.Open)
        {
            connection.Close();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

// Map health checks and return a JSON body (prevents some clients from erroring on an empty response)
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

app.Run();
