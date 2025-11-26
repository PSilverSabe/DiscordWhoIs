using Discord.Interactions;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Services;

namespace DiscordWhoIs.Commands
{
    public class ClearCacheCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Ao3FicFeedService _ao3;
        private readonly IPersistentCache _cache;

        public ClearCacheCommandModule(Ao3FicFeedService ao3, IPersistentCache persistentCache)
        {
            _ao3 = ao3;
            _cache = persistentCache;
        }

        [SlashCommand("clearcache", "Clear the AO3 fic cache")]
        public async Task ClearCacheAsync(
            [Summary("user", "AO3 username to remove from Cache")]string ao3Username
            )
        {
            // await DeferAsync(ephemeral: true); // Acknowledge the command
            var hasCacheKey = _cache.TryGetValue< IEnumerable<(string, string)>>(ao3Username, out _);

            if (hasCacheKey)
            {
                _cache.Remove(ao3Username);
                await FollowupAsync($"AO3 fic cache cleared for user {ao3Username}.", ephemeral: true);
            }

            await FollowupAsync("AO3 User Not Cached.", ephemeral: true);
        }

    }
}
