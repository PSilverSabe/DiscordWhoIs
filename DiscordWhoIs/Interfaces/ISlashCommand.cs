namespace DiscordWhoIs.Interfaces
{
    using Discord;
    using Discord.WebSocket;

    public interface ISlashCommand
    {
        string Name { get; }
        ApplicationCommandProperties Build();
        Task ExecuteAsync(SocketSlashCommand command);
    }
}
