using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.Helpers;

public static class InteractionResponseHelper
{
    public static Task UpdateOriginalResponseAsync(
        IDiscordInteraction interaction,
        List<string> lines,
        string insertContent,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(lines);

        lines.Add(insertContent);

        logger.LogInformation(insertContent);

        return interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = string.Join('\n', lines);
        });
    }
}
