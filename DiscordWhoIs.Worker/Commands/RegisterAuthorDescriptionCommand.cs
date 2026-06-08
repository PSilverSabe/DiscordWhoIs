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

    [SlashCommand("author-description", "Register a description. Call ao3-register beforehand.")]

    public async Task RegisterAo3AuthorDescriptionAsync(
    [Summary("Discord-User", description: "Register for another user (Admin Only)")]
    SocketGuildUser? user = null)
    {
        // Attempt to cast the calling user
        if (Context.User is not SocketGuildUser callingUser)
        {
            await RespondAsync(
                "This command must be used in a server.",
                ephemeral: true);
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

        // Open a modal for multi-line input
        ulong targetUserId = user?.Id ?? 0;

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
