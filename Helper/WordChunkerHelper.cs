namespace Task_corectev.core.Helpers
{
    public static class WordChunkerHelper
    {
        /// <summary>
        /// Splits text into overlapping chunks based on word count.
        /// </summary>
        /// <param name="text">The raw text to be chunked.</param>
        /// <param name="chunkSize">Maximum number of words per chunk.</param>
        /// <param name="overlap">Number of overlapping words between chunks.</param>
        /// <returns>A list of chunked strings.</returns>
        public static List<string> GetChunks(string text, int chunkSize = 150, int overlap = 30)
        {
            // Validation: Return empty list if text is null or empty
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            // Split text into words (handling spaces and new lines)
            var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int n = words.Length;

            // Validation: If total words are less than or equal to chunkSize, return the whole text as one chunk
            if (n <= chunkSize)
            {
                return new List<string> { string.Join(" ", words) };
            }

            var chunks = new List<string>();
            int stride = chunkSize - overlap;

            // Mathematical calculation for total chunks: Ceil((N - O) / S)
            int totalChunks = (int)Math.Ceiling((double)(n - overlap) / stride);

            for (int i = 0; i < totalChunks; i++)
            {
                int startIndex = i * stride;
                int remainingWords = n - startIndex;

                // Ensure we don't exceed the array bounds
                int currentChunkSize = Math.Min(chunkSize, remainingWords);

                var chunkWords = words.Skip(startIndex).Take(currentChunkSize);
                chunks.Add(string.Join(" ", chunkWords));

                // Break early if we've reached the end of the text
                if (startIndex + currentChunkSize >= n)
                {
                    break;
                }
            }

            return chunks;
        }
    }
}