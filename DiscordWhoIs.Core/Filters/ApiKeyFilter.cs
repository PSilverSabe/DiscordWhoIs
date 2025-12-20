using DiscordWhoIs.Core.Configuration.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DiscordWhoIs.Core.Filters
{
    public class ApiKeyFilter : IAsyncActionFilter
    {
        private readonly UploadConfiguration _uploadConfig;

        public ApiKeyFilter(UploadConfiguration uploadConfig)
        {
            _uploadConfig = uploadConfig;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) ||
                string.IsNullOrWhiteSpace(providedKey) ||
                providedKey != _uploadConfig.ApiKey)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            await next();
        }
    }
}