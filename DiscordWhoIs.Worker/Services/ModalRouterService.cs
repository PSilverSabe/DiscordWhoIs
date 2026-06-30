using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace DiscordWhoIs.Worker.Services;

public sealed class ModalRouterService
{
    private readonly Dictionary<string, Func<SocketModal, Task>> _handlers = new();

    public void Register(string customIdPrefix, Func<SocketModal, Task> handler)
        => _handlers[customIdPrefix] = handler;

    public async Task RouteAsync(SocketModal modal)
    {
        foreach (KeyValuePair<string, Func<SocketModal, Task>> handler in _handlers)
        {
            if (modal.Data.CustomId.StartsWith(handler.Key, StringComparison.OrdinalIgnoreCase))
            {
                await handler.Value(modal);
                return;
            }
        }
    }
}
