// File: core/Interfaces/ISpecializedAgent.cs
namespace Task_corectev.core.Interfaces
{
    public interface ISpecializedAgent
    {
        /// <summary>
        /// The unique name of the agent (e.g., "AtsAgent", "InterviewAgent").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A brief description of what this agent does (useful for the Router to know when to use it).
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Generates a response based on the agent's specific persona.
        /// </summary>
        /// <param name="userMessage">The user's specific question or input.</param>
        /// <param name="cvContext">The context retrieved from Qdrant.</param>
        /// <returns>The AI's specialized response.</returns>
        Task<string> GenerateResponseAsync(string userMessage, string cvContext);
    }
}