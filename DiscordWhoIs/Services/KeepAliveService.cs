namespace DiscordWhoIs.Services
{
    using Microsoft.Extensions.Hosting;
    public class KeepAliveService : IHostedService
    {
        private WebApplication? _app;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
            _app = builder.Build();

            _app.MapGet("/", () => "ok");
            _app.MapGet("/health", () => "healthy");

            await _app.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_app != null)
                await _app.StopAsync(cancellationToken);
        }
    }

}
