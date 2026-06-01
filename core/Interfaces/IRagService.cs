namespace Task_corectev.core.Interfaces
{
    public interface IRagService
    {
        /// <summary>
        /// Processes a CV: Extracts text, chunks it, generates embeddings, and saves to Qdrant.
        /// </summary>
        Task ProcessAndStoreCvAsync(IFormFile cvFile, string cvId);
        /// <summary>
        /// Searches Qdrant for the most relevant CV chunks based on the user's query.
        /// </summary>
        /// <param name="query">The user's question.</param>
        /// <param name="limit">Number of chunks to retrieve.</param>
        /// <returns>A combined string of the most relevant text chunks.</returns>
        Task<string> SearchCvAsync(string query, int limit = 3);
    }
}
