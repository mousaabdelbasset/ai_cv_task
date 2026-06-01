using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Task_corectev.core.Helpers;
using Task_corectev.core.Interfaces;

namespace Task_corectev.Infrastructure.Services
{
    public class RagService : IRagService
    {
        private readonly IPdfService _pdfService;
        private readonly ITextEmbeddingGenerationService _embeddingService;
        private readonly QdrantClient _qdrantClient;

        // The name of our collection in Qdrant
        private const string CollectionName = "test_rag";

        public RagService(
            IPdfService pdfService,
            ITextEmbeddingGenerationService embeddingService,
            QdrantClient qdrantClient)
        {
            _pdfService = pdfService;
            _embeddingService = embeddingService;
            _qdrantClient = qdrantClient;
        }

        public async Task ProcessAndStoreCvAsync(IFormFile cvFile, string cvId)
        {
            // 1. Extract Text from PDF (In-Memory)
            string rawText = await _pdfService.ExtractTextFromPdfAsync(cvFile);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                throw new InvalidOperationException("The uploaded CV contains no extractable text.");
            }

            // 2. Chunk the text using our custom math-based Word Chunker
            // 150 words per chunk, 30 words overlap
            List<string> chunks = WordChunkerHelper.GetChunks(rawText, 150, 30);

            // Ensure the collection exists in Qdrant (Creates it if it doesn't exist)
            await EnsureCollectionExistsAsync();

            // 3 & 4. Generate Embeddings and Store in Qdrant
            var points = new List<PointStruct>();

            for (int i = 0; i < chunks.Count; i++)
            {
                string currentChunk = chunks[i];

                // Generate embedding for the specific chunk using nomic-embed-text
                var embedding = await _embeddingService.GenerateEmbeddingAsync(currentChunk);

                // Convert ReadOnlyMemory<float> to float[] for Qdrant
                var embeddingArray = embedding.ToArray();

                // Create a unique ID for each chunk (combining the CV ID and the chunk index)
                // Qdrant requires a GUID or an unsigned integer for Point IDs
                Guid pointId = Guid.NewGuid();

                // Build the Qdrant point
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = pointId.ToString() },
                    Vectors = embeddingArray,
                    Payload =
                    {
                        // Store the actual text and CV ID in the payload so we can retrieve it later
                        ["cv_id"] = cvId,
                        ["chunk_index"] = i,
                        ["text"] = currentChunk
                    }
                };

                points.Add(point);
            }

            // Upsert (Insert or Update) the points into the Qdrant database
            await _qdrantClient.UpsertAsync(CollectionName, points);
        }

        // Helper method to setup the Qdrant Collection with the correct vector size
        private async Task EnsureCollectionExistsAsync()
        {
            bool collectionExists = await _qdrantClient.CollectionExistsAsync(CollectionName);

            if (!collectionExists)
            {
                // nomic-embed-text outputs vectors with 768 dimensions
                await _qdrantClient.CreateCollectionAsync(
                    collectionName: CollectionName,
                    vectorsConfig: new VectorParams { Size = 768, Distance = Distance.Cosine }
                );
            }
        }

        public async Task<string> SearchCvAsync(string query, int limit = 3)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            // 1. Convert the user's question into an embedding
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.ToArray();

            // 2. Search Qdrant for the closest matching vectors
            var searchResults = await _qdrantClient.SearchAsync(
                collectionName: CollectionName,
                vector: queryVector,
                limit: (ulong)limit
            );

            // 3. Extract the actual text from the retrieved payload
            var contextBuilder = new System.Text.StringBuilder();

            foreach (var result in searchResults)
            {
                // Retrieve the "text" field we saved earlier during upload
                if (result.Payload.TryGetValue("text", out var textValue))
                {
                    contextBuilder.AppendLine(textValue.StringValue);
                }
            }

            return contextBuilder.ToString();
        }
    }

}