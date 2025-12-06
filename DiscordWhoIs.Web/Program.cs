using DiscordWhoIs.Configuration;
using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Repositories;
using DiscordWhoIs.Databases.Serializers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Bind configurations
var fandomConfig = builder.Configuration.BindValidated<FandomConfiguration>("Fandom");
var discordConfig = builder.Configuration.BindValidated<DiscordConfiguration>("Discord");
var botDbContextConfig = builder.Configuration.BindValidated<FileLocationConfiguration>("BotDbContext");
var uploadConfig = builder.Configuration.BindValidated<UploadConfiguration>("Upload");

builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddMemoryCache();

// Environment-based database paths
var baseDir = AppContext.BaseDirectory;
var env = builder.Environment;
var botDbContext = env.IsDevelopment()
    ? Path.Combine(baseDir, "botdbcontext.sqlite")
    : Path.Combine(botDbContextConfig.TargetDirectory, botDbContextConfig.FileName) ?? Path.Combine(baseDir, "botdbcontext.sqlite");

// DbContext factory
builder.Services.AddDbContextFactory<BotDbContext>(options =>
    options.UseSqlite($"Data Source={botDbContext}"));

// Repositories
builder.Services.AddSingleton<AliasRepository>();
builder.Services.AddSingleton<DiscordWhoIs.Databases.Interfaces.IAliasRepository>(sp => sp.GetRequiredService<AliasRepository>());

builder.Services.AddSingleton<FanficRepository>();
builder.Services.AddSingleton<DiscordWhoIs.Databases.Interfaces.IFanficRepository>(sp => sp.GetRequiredService<FanficRepository>());

// Configure JSON serialization using source-generated context when available
var compositeResolver = new DiscordWhoIs.Databases.Serializers.CompositeJsonTypeInfoResolver(
    DiscordWhoIs.Databases.Serializers.ConfigurationJsonContext.Default,
    new DefaultJsonTypeInfoResolver());

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.TypeInfoResolver = compositeResolver;
    });

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
{
    opts.SerializerOptions.TypeInfoResolver = compositeResolver;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "DiscordWhoIs API", Version = "v1" }));

// Configs
builder.Services.AddSingleton(fandomConfig);
builder.Services.AddSingleton(discordConfig);
builder.Services.AddSingleton(botDbContextConfig);
builder.Services.AddSingleton(uploadConfig);

var app = builder.Build();

// Authentication middleware for /api
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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();