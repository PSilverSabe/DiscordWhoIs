namespace DiscordWhoIs.Databases.Interfaces
{
    public interface IRepository<T>
    {
        Task<IReadOnlyList<T>> GetAllAsync();
    }
}
