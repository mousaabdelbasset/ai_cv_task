namespace Task_corectev.Dtos
{
    public class ChatRequestDto
    {
        // Unique identifier for the user session to retrieve chat history from memory
        public string SessionId { get; set; } = string.Empty;

        // The actual message input from the user
        public string UserMessage { get; set; } = string.Empty;
    }
}
