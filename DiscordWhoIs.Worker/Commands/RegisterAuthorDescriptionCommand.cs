using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands;

public class RegisterAuthorDescriptionCommand(IAuthorRepository authorRepository,
    ILogger<WhoIsCommandModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAuthorRepository _author = authorRepository;
    private readonly ILogger<WhoIsCommandModule> _logger = logger;

    [SlashCommand("author-description", "Register a singular author description for an author. You must have called ao3-register beforehand")]

    public async Task RegisterAo3AuthorDescriptionAsync(
        [Summary("Description", description: "The Description for the User")]
        string description,
        [Summary("Discord-User", description: "Register for another user (Admin Only)")]
        SocketGuildUser? user = null
    )
    {
        await DeferAsync(ephemeral: true);
        var statusLines = new List<string> { };
        description = description.Trim();

        if (string.IsNullOrWhiteSpace(description))
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                "Description cannot be empty.", _logger);
            return;
        }

        if (Context.User is not SocketGuildUser guildUser)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                "This command must be used in a server (guild).", _logger);
            return;
        }

        // Check if calling user is admin
        bool isAdmin = guildUser.HasAdminPermissions();

        // Determine registration type
        bool isSelfRegister = user == null;

        // Determine if an admin is overriding another user's registration
        bool isAdminOverride = isAdmin && user != null;

        // Determine if a non-admin is trying to register for another user
        bool isUserTryingAdminOverride = !isAdmin && user != null;

        // Determine if an admin is registering for themselves
        bool AdminButSelfRegister = isAdmin && isSelfRegister;

        // Handle different registration scenarios
        // Admin overriding another user's registration
        if (isAdminOverride)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                $"Warning: The AO3 description may already be set. As you have administrative privileges, you will override this description.",
                _logger);
        }
        // Admin registering for themselves
        else if (AdminButSelfRegister)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                $"Warning: You are registering a Description to your own Discord user. " +
                "As you have administrative privileges, you will override this registration.", _logger);
        }
        // User registering for themselves
        else if (isSelfRegister)
        {
            statusLines.Add($"Attempting to register a description to your Discord user.");
        }
        // Non-admin trying to register for another user
        else if (isUserTryingAdminOverride)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                "You do not have permission to register AO3 authors for other Discord users.", _logger);
            return;
        }

        if (isAdminOverride)
        {
            guildUser = user!;
        }

        await _author.UpdateAuthorDescriptionAsync(guildUser.Id, description);

        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
            $"Successfully registered AO3 author description for {guildUser.Username}.", _logger);

        return;
    }
}
