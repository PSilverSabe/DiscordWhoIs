using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace DiscordWhoIs.Worker.Commands.Helpers;

public static class InteractionResponseHelper
{
    public static Task UpdateOriginalResponseAsync(
        IInteractionContext context,
        IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lines);

        return context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = string.Join('\n', lines);
        });
    }
}
