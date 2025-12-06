using DiscordWhoIs.Configuration.Models;
using DiscordWhoIs.Controllers.Models;
using DiscordWhoIs.Databases.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Files = System.IO.File;

namespace DiscordWhoIs.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class FicUpdateController : Controller
    {
        private readonly UploadConfiguration _uploadConfig;
        private readonly IFanficRepository _fanficRepository;
        private readonly IHostEnvironment _env;

        public FicUpdateController(
            UploadConfiguration uploadConfiguration,
            IFanficRepository fanficRepository,
            IHostEnvironment env)
        {
            _uploadConfig = uploadConfiguration;
            _fanficRepository = fanficRepository;
            _env = env;
        }

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
            // Return a concrete DTO (PingResponse) to avoid anonymous-type trimming issues.
            return Ok(new PingResponse("alive"));
        }

        // Hide only the problematic upload action from ApiExplorer so Swagger/ApiExplorer won't initialize ambiguous metadata for it.
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("file")]
        [RequestSizeLimit(100_000_000)]
        public async Task<ActionResult<OperationResult>> UploadUpdatedFanficCsvFile([FromForm] UploadFileRequest request)
        {
            var file = request?.File;
            if (file == null || file.Length == 0)
                return BadRequest(new OperationResult(false, "No file uploaded."));

            try
            {
                var uploadDir = GetResolvedUploadDirectory();
                Directory.CreateDirectory(uploadDir);

                await using var stream = new FileStream(GetResolvedUploadFilePath(), FileMode.Create);
                await file.CopyToAsync(stream);

                return Ok(new OperationResult(true, "File processed successfully."));
            }
            catch (Exception ex)
            {
                var msg = _uploadConfig.IncludeExceptionDetails ? $"Internal server error: {ex.Message}" : "Internal server error while processing the file.";
                return StatusCode(500, new OperationResult(false, msg));
            }
        }

        // Endpoint to trigger processing of the uploaded CSV into the DB.
        [HttpPost("update-db")]
        public async Task<ActionResult<OperationResult>> UpdateDatabaseFromCsvFile()
        {
            var filePath = GetResolvedUploadFilePath();
            if (!Files.Exists(filePath))
                return NotFound(new OperationResult(false, "CSV file not found."));

            try
            {
                await _fanficRepository.ImportFromCsvAsync(filePath);
                return Ok(new OperationResult(true, "Database updated successfully from CSV file."));
            }
            catch (Exception ex)
            {
                var msg = _uploadConfig.IncludeExceptionDetails ? $"Internal server error: {ex.Message}" : "Internal server error while updating the database.";
                return StatusCode(500, new OperationResult(false, msg));
            }
        }
    }
}
