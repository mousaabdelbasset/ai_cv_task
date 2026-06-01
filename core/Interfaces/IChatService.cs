using Task_corectev.Dtos;

namespace Task_corectev.core.Interfaces
{
    public interface IChatService
    {
        // Handles the chat completion process including history management and summarization
        Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto request);
    }
}
