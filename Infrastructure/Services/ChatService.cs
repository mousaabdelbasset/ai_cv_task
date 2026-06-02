// File: Infrastructure/Services/ChatService.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using Task_corectev.core.Interfaces;
using Task_corectev.Dtos;

namespace Task_corectev.Infrastructure.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatCompletionService _mainChatModel;
        private readonly IMemoryCache _memoryCache;
        private readonly ChatHistorySummarizationReducer _historyReducer;
        private readonly IRagService _ragService;
        private readonly IEnumerable<ISpecializedAgent> _agents;

        // Added logger to track user messages and agent routing
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            [FromKeyedServices("ChatModel")] IChatCompletionService mainChatModel,
            [FromKeyedServices("SummaryModel")] IChatCompletionService summaryModel,
            IMemoryCache memoryCache,
            IRagService ragService,
            IEnumerable<ISpecializedAgent> agents,
            ILogger<ChatService> logger)
        {
            _mainChatModel = mainChatModel;
            _memoryCache = memoryCache;
            _ragService = ragService;
            _agents = agents;
            _logger = logger;

            _historyReducer = new ChatHistorySummarizationReducer(summaryModel, targetCount: 10)
            {
                SummarizationInstructions = "Summarize the conversation briefly. Retain key career details.",
                FailOnError = false
            };
        }

        public async Task<ChatResponseDto> ProcessChatAsync(ChatRequestDto request)
        {
            // Track the incoming request and user message
            _logger.LogInformation("Processing chat request. SessionId: {SessionId}, CvId: {CvId}", request.SessionId, request.CvId);
            _logger.LogInformation("User Message: '{UserMessage}'", request.UserMessage);

            // 1. Retrieve the existing chat session from memory or create a new one
            if (!_memoryCache.TryGetValue(request.SessionId, out ChatHistory? chatHistory) || chatHistory == null)
            {
                chatHistory = new ChatHistory();
            }

            chatHistory.AddUserMessage(request.UserMessage);

            // 2. Intelligent Routing (Intent Classification)
            string targetAgentName = await DetermineTargetAgentAsync(request.UserMessage);

            // Track which agent was selected
            _logger.LogInformation("Message routed to Agent: {AgentName}", targetAgentName);

            // Find the requested agent from the injected list, fallback to AtsAgent if not found
            var selectedAgent = _agents.FirstOrDefault(a => a.Name == targetAgentName)
                                ?? _agents.First(a => a.Name == "AtsAgent");

            // 3. Orchestration: Search the Qdrant Vector DB
            // We now pass the request.CvId to filter the search context for this specific user
            string cvContext = await _ragService.SearchCvAsync(request.UserMessage, request.CvId);

            // 4. Context Augmentation: Pass context and recent history to the specialized agent
            string recentHistory = string.Join("\n", chatHistory.TakeLast(3).Select(m => $"{m.Role}: {m.Content}"));
            string contextForAgent = $"[Recent Conversation]:\n{recentHistory}\n\n[CV Context]:\n{cvContext}";

            string aiResponseText = await selectedAgent.GenerateResponseAsync(request.UserMessage, contextForAgent);

            _logger.LogInformation("AI generated a response successfully for SessionId: {SessionId}", request.SessionId);

            // 5. Save the generated response to the chat history
            chatHistory.AddAssistantMessage(aiResponseText);

            // 6. Reduce the history size if it exceeds the limit and save to cache
            var reducedMessages = await _historyReducer.ReduceAsync(chatHistory);
            if (reducedMessages != null)
            {
                chatHistory = new ChatHistory(reducedMessages);
            }

            var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(2));
            _memoryCache.Set(request.SessionId, chatHistory, cacheOptions);

            return new ChatResponseDto
            {
                AiResponse = aiResponseText,
                // HandledBy = selectedAgent.Name // Optional
            };
        }

        private async Task<string> DetermineTargetAgentAsync(string userMessage)
        {
            var availableAgentsInfo = string.Join("\n", _agents.Select(a => $"- {a.Name}: {a.Description}"));

            string routingPrompt = $@"You are an intelligent routing orchestrator.
Your job is to read the user's message and decide which specialized agent should handle it.

Available Agents:
{availableAgentsInfo}

User Message: ""{userMessage}""

CRITICAL INSTRUCTION: Reply ONLY with the exact Name of the chosen agent from the list above. Do not add any punctuation, explanation, or extra words. If unsure, reply with AtsAgent.";

            var routingHistory = new ChatHistory();
            routingHistory.AddSystemMessage(routingPrompt);

            var response = await _mainChatModel.GetChatMessageContentAsync(routingHistory);
            return response.Content?.Trim() ?? "AtsAgent";
        }
    }
}