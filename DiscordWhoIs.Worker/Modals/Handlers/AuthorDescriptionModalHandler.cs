using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using DiscordWhoIs.Worker.Extensions;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Modals.Handlers;

public sealed class AuthorDescriptionModalHandler(
    IAuthorRepository author,
    ILogger<AuthorDescriptionModalHandler> logger)
{
    private readonly IAuthorRepository _author = author;
    private readonly ILogger<AuthorDescriptionModalHandler> _logger = logger;

    public async Task HandleDescriptionAsyncViaModal(SocketModal modal)
    {
        if (!modal.Data.CustomId.StartsWith("author_description:"))
        {
            return;
        }

        ulong targetUserId = ulong.Parse(modal.Data.CustomId.Split(':')[1]);

        string description = modal.Data.Components
            .First(c => c.CustomId == "message")
            .Value
            .Trim();

        if (modal.User is not SocketGuildUser callingUser)
        {
            await modal.FollowupAsync(
                "This action must be used in a server.",
                ephemeral: true);
            return;
        }

        SocketGuildUser? targetUser = null;
        if (targetUserId != 0)
        {
            targetUser = callingUser.Guild.GetUser(targetUserId);
            if (targetUser == null)
            {
                await modal.FollowupAsync(
                    "Target user could not be resolved.",
                    ephemeral: true);
                return;
            }
        }

        await HandleDescriptionAsync(
            interaction: modal,
            callingUser: callingUser,
            targetUser: targetUser,
            description: description);
    }

    public async Task HandleDescriptionAsync(
        IDiscordInteraction interaction,
        string description,
        SocketGuildUser callingUser,
        SocketGuildUser? targetUser = null)
    {
        var statusLines = new List<string> { };
        try
        {
            await interaction.DeferAsync(ephemeral: true);
            description = description.Trim();

            if (string.IsNullOrWhiteSpace(description))
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(interaction, statusLines,
                    "Description cannot be empty.", _logger);
                return;
            }

            // Check if calling user is admin
            bool isAdmin = callingUser.HasAdminPermissions();

            // Determine registration type
            bool isSelfRegister = targetUser == null;

            // Determine if an admin is overriding another user's registration
            bool isAdminOverride = isAdmin && targetUser != null;

            // Determine if a non-admin is trying to register for another user
            bool isUserTryingAdminOverride = !isAdmin && targetUser != null;

            // Determine if an admin is registering for themselves
            bool AdminButSelfRegister = isAdmin && isSelfRegister;

            // Handle different registration scenarios
            // Admin overriding another user's registration
            if (isAdminOverride)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(interaction, statusLines,
                    $"Warning: The AO3 description may already be set. As you have administrative privileges, you will override this description.",
                    _logger);
            }
            // Admin registering for themselves
            else if (AdminButSelfRegister)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(interaction, statusLines,
                    $"Warning: You are registering a Description to your own Discord user. " +
                    "As you have administrative privileges, you will override this registration.", _logger);
            }
            // User registering for themselves
            else if (isSelfRegister)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(interaction, statusLines,
                    $"Attempting to register a description to your Discord user.", _logger);
            }
            // Non-admin trying to register for another user
            else if (isUserTryingAdminOverride)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(interaction, statusLines,
                    "**Only** Admins can use the **discord-user** parameter. " +
                    "You do not have permission to register AO3 authors for other Discord users. ", _logger);
                return;
            }

            if (isAdminOverride)
            {
                callingUser = targetUser!;
            }

            await _author.UpdateAuthorDescriptionAsync(callingUser.Id, description);

            await InteractionResponseHelper.UpdateOriginalResponseAsync(interaction, statusLines,
                $"Successfully registered AO3 author description for **{callingUser.Username}**.", _logger);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error handling author description modal or single line description.");
            throw;
        }
        finally
        {
            statusLines.Clear();
        }
    }

}

