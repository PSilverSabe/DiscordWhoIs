using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands;

public class RegisterAo3AuthorCommand(IAuthorRepository authorRepository,
    ILogger<WhoIsCommandModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAuthorRepository _author = authorRepository;
    private readonly ILogger<WhoIsCommandModule> _logger = logger;

    [SlashCommand("ao3-register", "Register ownership of an AO3 author name")]

    public async Task RegisterAo3AuthorAsync(
        [Summary("Ao3-Username", "Ao3 username or configured alias")]
        string authorName,
        [Summary("Discord-User", description: "Register for another user (Admin Only)")]
        SocketGuildUser? user = null
    )
    {
        await DeferAsync(ephemeral: true);
        var statusLines = new List<string> { };
        authorName = authorName.Trim();

        if (string.IsNullOrWhiteSpace(authorName))
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "AO3 author name cannot be empty.");
            return;
        }

        Author? existing = await _author.GetByAo3ProfileNameAsync(authorName);

        if (existing == null)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                $"The AO3 author name **{authorName}** does not exist in the database. " +
                "Please ensure you have at least one fanfic registered in the database before claiming ownership.");
            return;
        }

        if (Context.User is not SocketGuildUser guildUser)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines, "This command must be used in a server (guild).");
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

        // Prepare registration details
        int authorId = existing.AuthorId;
        ulong discordUserId = user?.Id ?? Context.User.Id;
        string discordUserName = user?.Username ?? Context.User.Username;

        // Handle different registration scenarios
        // Non-admin trying to register for another user
        if (isUserTryingAdminOverride)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                "You do not have permission to register AO3 authors for other Discord users.");
            return;
        }
        else
        {
            statusLines.Add($"Attempting to register AO3 author name **{authorName}** to your Discord user.");
        }

        // Check if Discord user is already registered
        if (await _author.DiscordIdAlreadyExists(discordUserId))
        {
            // If Admin is trying to register for themselves without [user], treat as self-register
            if (AdminButSelfRegister)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                    $"The current Discord user **{discordUserName}** is already registered to another AO3 author. " +
                    $"In order to override this registration, please use the 'Discord-User' parameter to register for another user.");
                return;
            }

            // Handle self-registration when already registered
            if (!isAdminOverride && isSelfRegister)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                    $"Your Discord user **{discordUserName}** is already registered to another AO3 author. " +
                    "Please contact an administrator if you believe this is an error.");
                return;
            }

            // Handle admin override registration
            if (isAdminOverride && !isSelfRegister)
            {
                await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
                    $"Warning: The Discord user **{discordUserName}** is already registered to another AO3 author. " +
                    "As you have administrative privileges, you will override this registration.");

                await _author.UpdateDiscordUsernameAsync(authorId, discordUserName, discordUserId, true);
                return;
            }
        }

        // Proceed with registration
        await _author.UpdateDiscordUsernameAsync(authorId, discordUserName, discordUserId);

        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines,
            $"Successfully registered AO3 author name **{authorName}** to Discord user **{discordUserName}**.");

        return;
    }
}
