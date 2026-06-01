// File: Api/Controllers/CvController.cs
using Microsoft.AspNetCore.Mvc;
using Task_corectev.core.Interfaces;

namespace Task_corectev.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CvController : ControllerBase
    {
        private readonly IRagService _ragService;

        public CvController(IRagService ragService)
        {
            _ragService = ragService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCv(IFormFile file)
        {
            // 1. Basic validation
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { Error = "Please upload a valid PDF file." });
            }

            if (file.ContentType != "application/pdf")
            {
                return BadRequest(new { Error = "Only PDF files are supported." });
            }

            try
            {
                // 2. Generate a unique ID for this CV
                // We generate it here so we can return it to the user or save it to a relational DB later if needed
                string cvId = Guid.NewGuid().ToString();

                // 3. Process the CV: Extract, Chunk, Embed, and Store in Qdrant
                await _ragService.ProcessAndStoreCvAsync(file, cvId);

                // 4. Return success response
                return Ok(new
                {
                    Message = "CV processed, chunked, embedded, and stored successfully in Qdrant.",
                    CvId = cvId
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "An unexpected error occurred while processing the CV.", Details = ex.Message });
            }
        }
    }
}