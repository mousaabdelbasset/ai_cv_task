using Microsoft.SemanticKernel.ChatCompletion;
using Task_corectev.core.Interfaces;

namespace Task_corectev.Infrastructure.Agents
{
    public class AtsAgent : ISpecializedAgent
    {
        private readonly IChatCompletionService _chatModel;

        // Implement the ISpecializedAgent properties
        public string Name => "AtsAgent";
        public string Description => "Expert in Applicant Tracking Systems (ATS), CV screening, career scoring, and identifying resume strengths and weaknesses.";

        // Inject the main ChatModel (mistral) specifically for this agent
        public AtsAgent([FromKeyedServices("ChatModel")] IChatCompletionService chatModel)
        {
            _chatModel = chatModel;
        }

        public async Task<string> GenerateResponseAsync(string userMessage, string cvContext)
        {
            // 1. Define a strict, expert persona system prompt for the ATS Agent
            var history = new ChatHistory();

            string atsSystemPrompt = @"You are a senior HR Executive and an ATS (Applicant Tracking System) Expert. 
                                      Your sole responsibility is to evaluate resumes, provide constructive feedback, calculate an ATS match score, and highlight gaps or missing keywords based on the market requirements.
                                      CRITICAL: You must answer using professional HR terminology and stay objective. Do not make up information that is not present in the CV context.";

            history.AddSystemMessage(atsSystemPrompt);

            // 2. Build the specialized prompt combining the user query and the retrieved CV chunks
            string specializedPrompt = $@"
[Target CV Data]:
{cvContext}

[User Request/Question]:
{userMessage}

Instructions: Analyze the [Target CV Data] above to answer the [User Request/Question]. Provide a well-structured response using bullet points where necessary.";

            history.AddUserMessage(specializedPrompt);

            // 3. Get the response from the LLM
            var response = await _chatModel.GetChatMessageContentAsync(history);
            return response.Content ?? "The ATS Agent was unable to analyze the CV.";
        }
    }
}