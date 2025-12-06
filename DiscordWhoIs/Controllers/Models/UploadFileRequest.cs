using Microsoft.AspNetCore.Http;

namespace DiscordWhoIs.Controllers.Models
{
    public sealed class UploadFileRequest
    {
        public IFormFile File { get; init; } = null!;
    }
}
