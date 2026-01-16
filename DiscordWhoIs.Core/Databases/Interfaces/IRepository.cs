namespace DiscordWhoIs.Core.Databases.Interfaces;

public interface IRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetAllAsync();
}
