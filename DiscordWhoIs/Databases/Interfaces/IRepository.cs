using DiscordWhoIs.Databases.DbModels;

namespace DiscordWhoIs.Databases.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<IReadOnlyList<T>> GetAllAsync();
    }
}
