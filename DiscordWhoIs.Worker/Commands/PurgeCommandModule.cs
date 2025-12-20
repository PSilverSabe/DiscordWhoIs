using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordWhoIs.Worker.Commands
{

    public class PurgeCommandModule
        : InteractionModuleBase<SocketInteractionContext>
    {
        private const int MaxPurge = 100;
        private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(30);

        [SlashCommand("purge", "Bulk delete messages (optionally from a specific user)")]
        [DefaultMemberPermissions(GuildPermission.ManageMessages)]
        public async Task PurgeAsync(
            [Summary("count", "Number of messages to delete (max 100)")]
            int count,
            [Summary("user", "Only delete messages from this user")]
            SocketGuildUser? user = null)
        {
            if (!HasManageMessages())
            {
                await RespondAsync(
                    "You do not have permission to manage messages.",
                    ephemeral: true);
                return;
            }

            if (count <= 0 || count > MaxPurge)
            {
                await RespondAsync(
                    "Count must be between 1 and 100.",
                    ephemeral: true);
                return;
            }

            if (Context.Channel is not ITextChannel)
            {
                await RespondAsync(
                    "This command can only be used in text channels.",
                    ephemeral: true);
                return;
            }

            var expiresAt = DateTimeOffset.UtcNow
                .Add(ConfirmationTimeout)
                .ToUnixTimeSeconds();

            var moderatorId = Context.User.Id;
            var targetUserId = user?.Id ?? 0;

            var confirmId =
                $"purge_confirm:{moderatorId}:{count}:{targetUserId}:{expiresAt}";
            var cancelId =
                $"purge_cancel:{moderatorId}:{expiresAt}";

            var components = new ComponentBuilder()
                .WithButton("Yes, delete", confirmId, ButtonStyle.Danger)
                .WithButton("Cancel", cancelId, ButtonStyle.Secondary)
                .Build();

            var description = user == null
                ? $"This will delete **{count} messages**."
                : $"This will delete **{count} messages** from **{user.Mention}**.";

            await RespondAsync(
                $"⚠️ **Confirm purge**\n{description}\n\n⏱ Expires in {ConfirmationTimeout.Seconds}s.",
                components: components,
                ephemeral: true);
        }

        [ComponentInteraction("purge_confirm:*")]
        public async Task ConfirmPurgeAsync(string[] args)
        {
            if (!HasManageMessages())
            {
                await RespondAsync(
                    "You do not have permission to manage messages.",
                    ephemeral: true);
                return;
            }

            var moderatorId = ulong.Parse(args[0]);
            var count = int.Parse(args[1]);
            var targetUserId = ulong.Parse(args[2]);
            var expiresAt =
                DateTimeOffset.FromUnixTimeSeconds(long.Parse(args[3]));

            if (Context.User.Id != moderatorId)
            {
                await RespondAsync(
                    "Only the moderator who started this purge can confirm it.",
                    ephemeral: true);
                return;
            }

            if (DateTimeOffset.UtcNow > expiresAt)
            {
                await DisableComponentsAsync("⏱ This purge request has expired.");
                return;
            }

            await DeferAsync(ephemeral: true);

            if (Context.Channel is not ITextChannel channel)
                return;

            var cutoff = DateTimeOffset.UtcNow.AddDays(-14);

            var messages = await channel
                .GetMessagesAsync(limit: 200)
                .FlattenAsync();

            var toDelete = messages
                .Where(m => m.Timestamp >= cutoff)
                .Where(m => targetUserId == 0 || m.Author.Id == targetUserId)
                .Take(count)
                .ToList();

            if (!toDelete.Any())
            {
                await FollowupAsync(
                    "No messages eligible for deletion (14-day limit).",
                    ephemeral: true);

                await DisableComponentsAsync("Nothing to delete.");
                return;
            }

            await channel.DeleteMessagesAsync(toDelete);

            await FollowupAsync(
                $"Deleted **{toDelete.Count} messages**.",
                ephemeral: true);

            await DisableComponentsAsync("✅ Purge completed.");
        }

        [ComponentInteraction("purge_cancel:*")]
        public async Task CancelPurgeAsync(string[] args)
        {
            var moderatorId = ulong.Parse(args[0]);
            var expiresAt =
                DateTimeOffset.FromUnixTimeSeconds(long.Parse(args[1]));

            if (Context.User.Id != moderatorId)
            {
                await RespondAsync(
                    "Only the moderator who started this purge can cancel it.",
                    ephemeral: true);
                return;
            }

            if (DateTimeOffset.UtcNow > expiresAt)
            {
                await DisableComponentsAsync("⏱ This purge request has expired.");
                return;
            }

            await DisableComponentsAsync("❌ Purge cancelled.");
        }

        private bool HasManageMessages()
        {
            return Context.User is SocketGuildUser user &&
                   user.GuildPermissions.ManageMessages;
        }

        private async Task DisableComponentsAsync(string message)
        {
            var disabled = new ComponentBuilder()
                .WithButton("Yes, delete", disabled: true)
                .WithButton("Cancel", disabled: true)
                .Build();

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Content = message;
                m.Components = disabled;
            });
        }
    }
}