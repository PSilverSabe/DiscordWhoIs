using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Worker.Constants;

namespace DiscordWhoIs.Worker.Commands;

public class PurgeCommand
    : InteractionModuleBase<SocketInteractionContext>
{
    private const int MaxPurge = 100;
    private static readonly TimeSpan s_confirmationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_ephemeralLifetime = TimeSpan.FromSeconds(5);

    [SlashCommand("purge", "Bulk delete messages (optionally from a specific user)")]
    [DefaultMemberPermissions(GuildPermission.ManageMessages)]
    public async Task PurgeAsync(
        [Summary("Count", "Number of messages to delete (max 100)")]
        int count,
        [Summary("User", "Only delete messages from this user")]
        SocketGuildUser? user = null
    )
    {
        await DeferAsync(ephemeral: true);

        if (!HasManageMessages())
        {
            await FollowupAsync(
                "You do not have permission to manage messages.",
                ephemeral: true);
            return;
        }

        if (count <= 0 || count > MaxPurge)
        {
            await FollowupAsync(
                "Count must be between 1 and 100.",
                ephemeral: true);
            return;
        }

        if (Context.Channel is not ITextChannel)
        {
            await FollowupAsync(
                "This command can only be used in text channels.",
                ephemeral: true);
            return;
        }

        long expiresAt = DateTimeOffset.UtcNow
            .Add(s_confirmationTimeout)
            .ToUnixTimeSeconds();

        ulong moderatorId = Context.User.Id;
        ulong targetUserId = user?.Id ?? 0;

        string confirmId =
            $"purge_confirm:{moderatorId}:{count}:{targetUserId}:{expiresAt}";
        string cancelId =
            $"purge_cancel:{moderatorId}:{expiresAt}";

        MessageComponent components = new ComponentBuilder()
            .WithButton("Yes, delete", confirmId, ButtonStyle.Danger)
            .WithButton("Cancel", cancelId, ButtonStyle.Secondary)
            .Build();

        string description = user == null
            ? $"This will delete **{count} messages**."
            : $"This will delete **{count} messages** from **{user.Mention}**.";

        await FollowupAsync(
            $"⚠️ **Confirm purge**\n{description}\n\n⏱ Expires in {s_confirmationTimeout.Seconds}s.",
            components: components,
            ephemeral: true);

        await Task.Delay(s_ephemeralLifetime);

        await Context.Interaction.DeleteOriginalResponseAsync();
    }

    [ComponentInteraction("purge_confirm:*")]
    public async Task ConfirmPurgeAsync(string data)
    {
        string[] args = data.Split(':');
        if (!HasManageMessages())
        {
            await FollowupAsync(
                "You do not have permission to manage messages.",
                ephemeral: true);
            return;
        }

        ulong moderatorId = ulong.Parse(args[0]);
        int count = int.Parse(args[1]);
        ulong targetUserId = ulong.Parse(args[2]);
        var expiresAt =
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(args[3]));

        if (Context.User.Id != moderatorId)
        {
            await FollowupAsync(
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
        {
            return;
        }

        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-14);

        IEnumerable<IMessage> messages = await channel
            .GetMessagesAsync(limit: WorkerConstants.DiscordGetMessagesLimit)
            .FlattenAsync();

        var toDelete = messages
            .Where(m => m.Timestamp >= cutoff)
            .Where(m => targetUserId == 0 || m.Author.Id == targetUserId)
            .Take(count)
            .ToList();

        if (toDelete.Count == 0)
        {
            IUserMessage msg = await FollowupAsync(
                "No messages eligible for deletion (14-day limit).",
                ephemeral: true);
            _ = DeleteAfterDelay(msg);

            await DisableComponentsAsync("Nothing to delete.");
            return;
        }

        await channel.DeleteMessagesAsync(toDelete);

        IUserMessage followUp = await FollowupAsync(
            $"Deleted **{toDelete.Count} messages**.",
            ephemeral: true);
        _ = DeleteAfterDelay(followUp);

        await DisableComponentsAsync("✅ Purge completed.");
    }

    [ComponentInteraction("purge_cancel:*")]
    public async Task CancelPurgeAsync(string data)
    {
        string[] args = data.Split(':');
        ulong moderatorId = ulong.Parse(args[0]);
        var expiresAt =
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(args[1]));

        if (Context.User.Id != moderatorId)
        {
            await FollowupAsync(
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

    private bool HasManageMessages() => Context.User is SocketGuildUser user &&
               user.GuildPermissions.ManageMessages;

    private async Task DisableComponentsAsync(string message)
    {
        MessageComponent disabled = new ComponentBuilder()
            .WithButton("Yes, delete", disabled: true)
            .WithButton("Cancel", disabled: true)
            .Build();

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Content = message;
            m.Components = disabled;
        });

        // Fetch the modified original response and delete after 5 seconds
        if (await Context.Interaction.GetOriginalResponseAsync() is IUserMessage msg)
        {
            _ = DeleteAfterDelay(msg);
        }
    }

    // ------------------------------
    // Helper to delete ephemeral messages after 5 seconds
    // ------------------------------
    private static async Task DeleteAfterDelay(IUserMessage message)
    {
        try
        {
            await Task.Delay(s_ephemeralLifetime);
            await message.DeleteAsync();
        }
        catch
        {
            // Ignore errors if already deleted
        }
    }
}
