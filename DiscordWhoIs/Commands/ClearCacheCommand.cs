using Discord.Interactions;
using DiscordWhoIs.Databases.DataModels;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Services;

namespace DiscordWhoIs.Commands
{
    public class ClearCacheCommandModule(Ao3FicFeedService Ao3, IPersistentCache persistentCache) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Ao3FicFeedService _Ao3 = Ao3;
        private readonly IPersistentCache _cache = persistentCache;

        [SlashCommand("clearcache", "Clear the Ao3 fic cache")]
        public async Task ClearCacheAsync(
            [Summary("user", "Ao3 username to remove from Cache")] string Ao3Username
            )
        {
            await DeferAsync(ephemeral: true); // Acknowledge the command
            var hasCacheKey = _cache.TryGetValue($"{Ao3Username}", out _);

            if (hasCacheKey)
            {
                await _cache.RemoveAsync(Ao3Username);
                hasCacheKey = _cache.TryGetValue($"{Ao3Username}", out _);
                if (hasCacheKey)
                {
                    await FollowupAsync($"Ao3 cache cleared for user {Ao3Username}.", ephemeral: true);
                    return;
                }
                else
                {
                    await FollowupAsync($"Ao3 cache was cleared but still persisted, shit.", ephemeral: true);
                }
            }

            await FollowupAsync($"``{Ao3Username}`` Not Cached.", ephemeral: true);
        }
    }
}
