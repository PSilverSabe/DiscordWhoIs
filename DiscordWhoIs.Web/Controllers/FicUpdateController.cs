using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.Interfaces;
using DiscordWhoIs.Core.Filters;
using DiscordWhoIs.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Files = System.IO.File;

namespace DiscordWhoIs.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(ApiKeyFilter))]
public class FicUpdateController(
    UploadConfiguration uploadConfiguration,
    IFanficRepository fanficRepository,
    IHostEnvironment env,
    ILogger<FicUpdateController> logger) : Controller
{
    private readonly UploadConfiguration _uploadConfig = uploadConfiguration;
    private readonly IFanficRepository _fanficRepository = fanficRepository;
    private readonly IHostEnvironment _env = env;
    private readonly ILogger<FicUpdateController> _logger = logger;

    private string GetResolvedUploadFilePath() => PathResolver.ResolvePath(
            _uploadConfig.TargetDirectory,
            _uploadConfig.FileName ?? "fanfic_updates.json"
        );

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "alive" });

    [HttpPost("file")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> UploadUpdatedFanficJsonFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GetResolvedUploadFilePath())!);

            string filePath = GetResolvedUploadFilePath();
            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream);

            _logger.LogInformation("Uploaded file saved to {FilePath} (size: {Size})", filePath, file.Length);

            return Ok("File processed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file for fanfic updates");
            return _uploadConfig.IncludeExceptionDetails
                ? StatusCode(500, $"Internal server error: {ex.Message}")
                : StatusCode(500, "Internal server error while processing the file.");
        }
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateDatabaseFromJsonFile()
    {
        string filePath = GetResolvedUploadFilePath();

        if (!Files.Exists(filePath))
        {
            _logger.LogWarning("Attempted to update database but JSON file not found at {FilePath}", filePath);
            return NotFound("JSON file not found.");
        }

        try
        {
            _logger.LogInformation("Starting database update from JSON file {FilePath}", filePath);
            await _fanficRepository.ImportFromJsonAsync(filePath);
            _logger.LogInformation("Database update from JSON file completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update database from JSON file {FilePath}", filePath);
            return _uploadConfig.IncludeExceptionDetails
                ? StatusCode(500, $"Internal server error: {ex.Message}")
                : StatusCode(500, "Internal server error while updating the database.");
        }

        return Ok("Database updated successfully from JSON file.");
    }
}
