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
            existing = await _author.GetByDiscordIdAsync(user.Id);

            statusLines.Add($"Warning: The AO3 author name **{existing?.Ao3ProfileName}** is already registered to a Discord user (ID: {user.Id}). " +
                 "As you have administrative privileges, you will override the existing description.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
        }

        user ??= guildUser;
        existing ??= await _author.GetByDiscordIdAsync(user.Id);

        if (existing == null)
        {
            statusLines.Add($"No AO3 author is registered for Discord user **{user.Mention}** (ID: {user.Id}). " +
                             "Please have them register first using the `/ao3-register` command.");
            await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
            return;
        }

        await _author.UpdateAuthorDescriptionAsync(existing.AuthorId, description);

        statusLines.Add($"Successfully updated the description for AO3 author **{existing.Ao3ProfileName}** " +
                         $"(Discord User: **{user.Username}**, ID: {user.Id}).");
        await InteractionResponseHelper.UpdateOriginalResponseAsync(Context, statusLines);
    }
}
