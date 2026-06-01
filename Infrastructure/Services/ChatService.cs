// File: Infrastructure/Services/ChatService.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using Task_corectev.core.Interfaces;
using Task_corectev.Dtos;

namespace Task_corectev.Infrastructure.Services
{
    public class ChatService : IChatService
    {
        // The main heavy model (mistral) used for generating responses
        private readonly IChatCompletionService _mainChatModel;
        private readonly IMemoryCache _memoryCache;
        private readonly ChatHistorySummarizationReducer _historyReducer;

        // The RAG service used to search Qdrant for CV context
        private readonly IRagService _ragService;

        public ChatService(
            [FromKeyedServices("ChatModel")] IChatCompletionService mainChatModel,
            [FromKeyedServices("SummaryModel")] IChatCompletionService summaryModel,
            IMemoryCache memoryCache,
            IRagService ragService)
        {
            _mainChatModel = mainChatModel;
            _memoryCache = memoryCache;
            _ragService = ragService;

            // Initialize the reducer with the lightweight model (llama2) to manage token limits safely
            _historyReducer = new ChatHistorySummarizationReducer(summaryModel, targetCount: 10)
            {
                SummarizationInstructions = "Summarize the following conversation briefly. CRITICAL: Retain all important career details, skills, and context.",
                FailOnError = false
            };
        }

        public async Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto request)
        {
            // 1. Retrieve the existing chat session from memory or create a new one
            if (!_memoryCache.TryGetValue(request.SessionId, out ChatHistory? chatHistory) || chatHistory == null)
            {
                chatHistory = new ChatHistory();

                // Strict System Prompt to enforce the HR Assistant persona and prevent hallucination
                string systemPrompt = @"You are an expert AI HR Assistant. 
                                        You analyze CVs and answer career-related questions. 
                                        If the user asks outside this scope, decline politely. 
                                        Always base your answers strictly on the provided CV context.";
                chatHistory.AddSystemMessage(systemPrompt);
            }

            // 2. Orchestration: Search the Qdrant Vector DB based on the user's query
            string cvContext = await _ragService.SearchCvAsync(request.UserMessage);

            // 3. Context Augmentation: Combine the user's message with the retrieved CV data
            string enrichedUserMessage = request.UserMessage;

            if (!string.IsNullOrWhiteSpace(cvContext))
            {
                enrichedUserMessage = $@"
User Question: {request.UserMessage}

[Context extracted from the CV]:
{cvContext}

(Instructions: Answer the user's question based strictly on the CV Context above. If the answer is not in the context, clearly state that the CV does not contain this information.)";
            }

            // 4. Add the enriched message to the chat history
            chatHistory.AddUserMessage(enrichedUserMessage);

            // 5. Reduce the history if it exceeds the target count to save context window tokens
            var reducedMessages = await _historyReducer.ReduceAsync(chatHistory);
            if (reducedMessages != null)
            {
                chatHistory = new ChatHistory(reducedMessages);
            }

            // 6. Call the main LLM (mistral) to generate the final response
            var response = await _mainChatModel.GetChatMessageContentAsync(chatHistory);
            string aiResponseText = response.Content ?? "I am sorry, I could not generate a response.";

            // 7. Add the AI's response to the history to maintain the conversational context
            chatHistory.AddAssistantMessage(aiResponseText);

            // 8. Save the updated history back to the memory cache with a 2-hour expiration
            var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(2));
            _memoryCache.Set(request.SessionId, chatHistory, cacheOptions);

            // 9. Return the formatted DTO object
            return new ChatResponseDto
            {
                AiResponse = aiResponseText
            };
        }
    }
}