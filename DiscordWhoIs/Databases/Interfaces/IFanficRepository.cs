using DiscordWhoIs.Databases.DbModels;

namespace DiscordWhoIs.Databases.Interfaces
{
    public interface IFanficRepository : IRepository<Fanfic>
    {
        Task<IReadOnlyList<Fanfic>> GetAllByAuthorAsync(string authorName);

        Task<Fanfic?> GetByTitleAsync(string title);

        Task<Fanfic?> GetByIdAsync(int id);

        Task<bool> ImportFromCsvAsync(string csvFileName);
    }
}
