using DiscordWhoIs.Core.Extensions;


var builder = WebApplication.CreateBuilder(args);

// Register Core services (DbContextFactory + repositories)
builder.Services.AddDiscordWhoIsCore(builder.Configuration);

builder.Services.AddControllers(); // your normal web registrations
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();