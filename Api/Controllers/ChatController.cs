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

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequestDto request)
        {
            // 1. Basic Validation: Ensure the request and its properties are not empty
            if (request == null || string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.UserMessage))
            {
                return BadRequest(new { Error = "SessionId and UserMessage are required." });
            }

            try
            {
                // 2. Process the chat using our service
                var response = await _chatService.ProcessChatAsync(request);

                // 3. Return the ChatResponseDto as a successful response
                return Ok(response);
            }
            catch (Exception ex)
            {
                // 4. Handle unexpected errors gracefully
                // In a production environment, you should log 'ex' using an ILogger
                return StatusCode(500, new { Error = "An unexpected error occurred while processing your request.", Details = ex.Message });
            }
        }
    }
}