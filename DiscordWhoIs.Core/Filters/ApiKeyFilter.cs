using DiscordWhoIs.Core.Configuration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace DiscordWhoIs.Core.Filters;

public class ApiKeyFilter(UploadConfiguration uploadConfig, ILogger<ApiKeyFilter> logger) : IAsyncActionFilter
{
    private readonly UploadConfiguration _uploadConfig = uploadConfig;
    private readonly ILogger<ApiKeyFilter> _logger = logger;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out StringValues providedKeyValues) ||
            string.IsNullOrWhiteSpace(providedKeyValues))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        string providedKey = providedKeyValues.ToString();

        if (!_uploadConfig.ValidateApiKey(providedKey))
        {
            _logger.LogWarning("Unauthorized API key access attempt from {RemoteIp}",
                context.HttpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
