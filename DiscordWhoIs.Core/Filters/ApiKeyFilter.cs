using DiscordWhoIs.Core.Configuration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace DiscordWhoIs.Core.Filters;

public class ApiKeyFilter(UploadConfiguration uploadConfig) : IAsyncActionFilter
{
    private readonly UploadConfiguration _uploadConfig = uploadConfig;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out StringValues providedKey) ||
            string.IsNullOrWhiteSpace(providedKey) ||
            providedKey != _uploadConfig.ApiKey)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}