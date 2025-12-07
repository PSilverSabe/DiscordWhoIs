using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Core.Filters;
using DiscordWhoIs.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Files = System.IO.File;

namespace DiscordWhoIs.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [TypeFilter(typeof(ApiKeyFilter))]
    public class FicUpdateController(UploadConfiguration uploadConfiguration, IFanficRepository fanficRepository, IHostEnvironment env) : Controller
    {
        private readonly UploadConfiguration _uploadConfig = uploadConfiguration;
        private readonly IFanficRepository _fanficRepository = fanficRepository;
        private readonly IHostEnvironment _env = env;

        private string GetResolvedUploadFilePath()
        {
            return PathResolver.ResolvePath(
                _uploadConfig.TargetDirectory,
                _uploadConfig.FileName ?? "fanfic_updates.csv"
            );
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "alive" });
        }

        [HttpPost("file")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UploadUpdatedFanficCsvFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GetResolvedUploadFilePath())!);

                var filePath = GetResolvedUploadFilePath();
                await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await file.CopyToAsync(stream);

                return Ok("File processed successfully.");
            }
            catch (Exception ex)
            {
                return _uploadConfig.IncludeExceptionDetails
                    ? StatusCode(500, $"Internal server error: {ex.Message}")
                    : StatusCode(500, "Internal server error while processing the file.");
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateDatabaseFromCsvFile()
        {
            var filePath = GetResolvedUploadFilePath();

            if (!Files.Exists(filePath))
                return NotFound("CSV file not found.");

            try
            {
                await _fanficRepository.ImportFromCsvAsync(filePath);
            }
            catch (Exception ex)
            {
                return _uploadConfig.IncludeExceptionDetails
                    ? StatusCode(500, $"Internal server error: {ex.Message}")
                    : StatusCode(500, "Internal server error while updating the database.");
            }

            return Ok("Database updated successfully from CSV file.");
        }
    }
}