using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Databases.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Files = System.IO.File;

namespace DiscordWhoIs.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class FicUpdateController(UploadConfiguration uploadConfiguration, IFanficRepository fanficRepository, IHostEnvironment env) : Controller
    {
        private readonly UploadConfiguration _uploadConfig = uploadConfiguration;
        private readonly IFanficRepository _fanficRepository = fanficRepository;
        private readonly IHostEnvironment _env = env;

        private string GetResolvedUploadDirectory()
        {
            if (_env.IsDevelopment())
            {
                return Path.Combine(AppContext.BaseDirectory, _uploadConfig.TargetDirectory);
            }

            return Path.Combine(_uploadConfig.TargetDirectory, _uploadConfig.FileName);
        }

        private string GetResolvedUploadFilePath()
        {
            if (_env.IsDevelopment())
            {
                return Path.Combine(AppContext.BaseDirectory, _uploadConfig.TargetDirectory, _uploadConfig.FileName);
            }

            return Path.Combine(_uploadConfig.TargetDirectory, _uploadConfig.FileName);
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
            {
                return BadRequest("No file uploaded.");
            }

            try
            {
                var uploadDir = GetResolvedUploadDirectory();
                Directory.CreateDirectory(uploadDir);

                using var stream = new FileStream(GetResolvedUploadFilePath(), FileMode.Create);
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

        public async Task<IActionResult> UpdateDatabaseFromCsvFile()
        {
            var filePath = GetResolvedUploadFilePath();
            if (Files.Exists(GetResolvedUploadFilePath()))
            {
                return NotFound("CSV file not found.");
            }
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
