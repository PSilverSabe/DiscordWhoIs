using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Interactions;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Worker.Commands.Helpers;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Worker.Commands.EmbedPoster;

public class EmbedPosterSetDeduplicationCommand(
    IEmbedPosterConfigurationRepository configRepository,
    ILogger<EmbedPosterSetDeduplicationCommand> logger)
    : EmbedPosterCommandGroup
{
    private readonly IEmbedPosterConfigurationRepository _configRepository = configRepository;
    private readonly ILogger<EmbedPosterSetDeduplicationCommand> _logger = logger;

    [SlashCommand("set-deduplication-window", "Set how long to suppress duplicate embeds for the same link")]
    public async Task SetDeduplicationWindowAsync(
        [Summary("Minutes", "Number of minutes to suppress duplicate embeds (1–1440)")]
        int minutes)
    {
        var statusLines = new List<string>();
        await DeferAsync(ephemeral: true);

        if (minutes < 1 || minutes > 1440)
        {
            await InteractionResponseHelper.UpdateOriginalResponseAsync(
                Context.Interaction, statusLines,
                "Minutes must be between 1 and 1440 (24 hours).",
                _logger);
            return;
        }

        await _configRepository.SetDeduplicationWindowAsync(minutes);

        await InteractionResponseHelper.UpdateOriginalResponseAsync(
            Context.Interaction, statusLines,
            $"✅ Deduplication window set to **{minutes} minute(s)**.",
            _logger);
    }
}
