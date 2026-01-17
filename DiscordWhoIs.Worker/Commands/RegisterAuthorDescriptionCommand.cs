using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Modals.Handlers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands;

public class RegisterAuthorDescriptionCommand(
    IAuthorRepository authorRepository,
    ILogger<WhoIsCommandModule> logger,
    AuthorDescriptionModalHandler authorDescriptionModalHandler) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAuthorRepository _author = authorRepository;
    private readonly ILogger<WhoIsCommandModule> _logger = logger;
    private readonly AuthorDescriptionModalHandler _authorDescriptionModalHandler = authorDescriptionModalHandler;

    [SlashCommand(
        "author-description", "Register a description. For single-line descriptions, use parameter. Call ao3-register beforehand."
        )]

    public async Task RegisterAo3AuthorDescriptionAsync(
    [Summary("description", description: "Single-line description (optional)")]
    string? description = null,
    [Summary("discord-user", description: "Register for another user (Admin only)")]
    SocketGuildUser? user = null)
    {
        // Attempt to cast the calling user
        var callingUser = Context.User as SocketGuildUser;
        if (callingUser == null)
        {
            await RespondAsync(
                "This command must be used in a server.",
                ephemeral: true);
            return;
        }

        // If the user provided a single-line description, process immediately
        if (!string.IsNullOrWhiteSpace(description))
        {
            // Determine the target user
            SocketGuildUser targetUser = user ?? callingUser;

            // Call shared handler method
            await _authorDescriptionModalHandler.HandleDescriptionAsync(
                interaction: Context.Interaction,
                callingUser: callingUser,
                targetUser: targetUser,
                description: description.Trim());

            return;
        }

        Author? author = await _author.GetByDiscordIdAsync(user?.Id ?? callingUser.Id);
        if (author == null)
        {
            await RespondAsync(
                "No AO3 author is registered for the specified user. Please register an author first using /ao3-register.",
                ephemeral: true);
            return;
        }

        // Pre-fill existing description if available
        string existingDescription = author.Description ?? string.Empty;

        // Otherwise, open a modal for multi-line input
        ulong targetUserId = user?.Id ?? callingUser.Id;

        Modal modal = new ModalBuilder()
            .WithTitle("Register Author Description")
            .WithCustomId($"author_description:{targetUserId}")
            .AddTextInput(
                label: "Author Description",
                customId: "message",
                style: TextInputStyle.Paragraph,
                placeholder: "Enter the author description",
                maxLength: 500,
                required: true,
                value: existingDescription)
            .Build();

        await RespondWithModalAsync(modal);
    }
}
