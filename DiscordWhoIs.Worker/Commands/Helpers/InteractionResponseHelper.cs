using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.Helpers;

public static class InteractionResponseHelper
{
    public static Task UpdateOriginalResponseAsync(
        IInteractionContext context,
        List<string> lines,
        string insertContent,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lines);

        lines.Add(insertContent);

        logger.LogInformation(insertContent);

        return context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = string.Join('\n', lines);
        });
    }
}
