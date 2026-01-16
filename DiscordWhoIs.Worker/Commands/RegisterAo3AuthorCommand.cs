using System.Collections.Generic;
using System.Linq;
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
        var statusLines = new List<string> { };
        await DeferAsync(ephemeral: true);

        if (Context.User is not SocketGuildUser guildUser)
        {
            statusLines.Add("This command must be used in a server (guild).");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        bool isAdmin = guildUser.GuildPermissions.Administrator
          || guildUser.GuildPermissions.ManageGuild
          || guildUser.Roles.Any(r => r.Id == 1358267994303889589);

        if (!isAdmin && user != null)
        {
            statusLines.Add("You do not have permission to register AO3 authors for other Discord users.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        Author? existing = null;

        if (isAdmin && user != null)
        {
            existing = await _author.GetByAo3ProfileNameAsync(authorName);

            statusLines.Add($"Warning: The AO3 author name **{authorName}** is already registered to a Discord user (ID: {existing.DiscordId}). " +
                             "As you have administrative privileges, you are overriding this registration.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
        }

        user ??= guildUser;
        authorName = authorName.Trim();

        if (string.IsNullOrWhiteSpace(authorName))
        {
            statusLines.Add("AO3 author name cannot be empty.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        if (existing == null)
        {
            statusLines.Add($"The AO3 author name **{authorName}** does not exist in the database. " +
                             "Please ensure you have at least one fanfic registered in the database before claiming ownership.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        if (existing.DiscordId != null && !isAdmin)
        {
            statusLines.Add($"The AO3 author name **{authorName}** has already been registered.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        if (existing.DiscordId != null && isAdmin)
        {
            statusLines.Add($"Warning: The AO3 author name **{authorName}** is already registered to a Discord user (ID: {existing.DiscordId}). " +
                             "As you have administrative privileges, you are overriding this registration.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
        }

        await _author.UpdateDiscordUsernameAsync(existing.AuthorId, user.Username, user.Id);

        statusLines.Add($"Successfully registered AO3 author **{existing.Ao3ProfileName}** to (Discord User: **{user.Username}**, ID: {user.Id}).");
        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);

    }
}
