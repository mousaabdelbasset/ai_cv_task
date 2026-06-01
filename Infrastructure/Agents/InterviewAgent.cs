// File: Infrastructure/Agents/InterviewAgent.cs
using Microsoft.SemanticKernel.ChatCompletion;
using Task_corectev.core.Interfaces;

namespace Task_corectev.Infrastructure.Agents
{
    public class InterviewAgent : ISpecializedAgent
    {
        private readonly IChatCompletionService _chatModel;

        public string Name => "InterviewAgent";
        public string Description => "Expert Technical and HR Interviewer. Conducts mock interviews, asks targeted questions based on the candidate's CV, and evaluates their answers.";

        public InterviewAgent([FromKeyedServices("ChatModel")] IChatCompletionService chatModel)
        {
            _chatModel = chatModel;
        }

        public async Task<string> GenerateResponseAsync(string userMessage, string cvContext)
        {
            var history = new ChatHistory();

            // 1. The Interviewer Persona
            string interviewSystemPrompt = @"You are a professional, tough but fair Technical and HR Interviewer. 
                                      Your job is to conduct a mock interview based ONLY on the skills and experiences mentioned in the provided CV. 
                                      CRITICAL RULES:
                                      - Ask only ONE question at a time.
                                      - Evaluate the user's previous answer (if any) before asking the next question.
                                      - Keep the tone professional and conversational.";

            history.AddSystemMessage(interviewSystemPrompt);

            // 2. The Specialized Prompt
            string specializedPrompt = $@"
[Candidate CV Data]:
{cvContext}

[User Input]:
{userMessage}

Instructions: Respond to the user's input. If they are just saying 'hello' or starting the interview, welcome them and ask the very first interview question based on their [Candidate CV Data]. If they are answering a previous question, evaluate their answer briefly and ask the next logical question.";

            history.AddUserMessage(specializedPrompt);

            // 3. Call the LLM
            var response = await _chatModel.GetChatMessageContentAsync(history);
            return response.Content ?? "The Interview Agent is currently unavailable.";
        }
    }
}