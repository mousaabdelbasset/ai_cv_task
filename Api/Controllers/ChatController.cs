// File: Api/Controllers/ChatController.cs
using Microsoft.AspNetCore.Mvc;
using Task_corectev.core.Interfaces;
using Task_corectev.Dtos;

namespace Task_corectev.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequestDto request)
        {
            // 1. Basic Validation: Ensure the request and its properties are not empty
            if (request == null || string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.UserMessage))
            {
                _logger.LogWarning("Invalid chat request received. Missing SessionId or UserMessage.");
                return BadRequest(new { Error = "SessionId and UserMessage are required." });
            }

            // 2. Ensure CvId is provided to avoid cross-CV data leakage
            if (string.IsNullOrWhiteSpace(request.CvId))
            {
                _logger.LogWarning("Invalid chat request received. Missing CvId.");
                return BadRequest(new { Error = "CvId is required to search within a specific CV." });
            }

            try
            {
                _logger.LogInformation("Receiving Chat Request for Session: {SessionId}, CV: {CvId}", request.SessionId, request.CvId);

                // 3. Process the chat using our service
                var response = await _chatService.ProcessChatAsync(request);

                return Ok(response);
            }
            catch (Exception ex)
            {
                // 4. Handle unexpected errors gracefully and log the actual error details
                _logger.LogError(ex, "An unexpected error occurred while processing the chat request for SessionId: {SessionId}", request.SessionId);
                return StatusCode(500, new { Error = "An unexpected error occurred while processing your request.", Details = ex.Message });
            }
        }
    }
}