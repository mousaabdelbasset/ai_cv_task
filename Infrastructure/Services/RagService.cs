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
        private readonly ILogger<RagService> _logger;

        // The name of our collection in Qdrant
        private const string CollectionName = "ResumeContainer";

        public RagService(
            IPdfService pdfService,
            ITextEmbeddingGenerationService embeddingService,
            QdrantClient qdrantClient,
            ILogger<RagService> logger)
        {
            _pdfService = pdfService;
            _embeddingService = embeddingService;
            _qdrantClient = qdrantClient;
            _logger = logger;
        }

        public async Task ProcessAndStoreCvAsync(IFormFile cvFile, string cvId)
        {
            _logger.LogInformation("Starting CV processing and storing. CV ID: {CvId}", cvId);

            // 1. Extract Text from PDF (In-Memory)
            string rawText = await _pdfService.ExtractTextFromPdfAsync(cvFile);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                _logger.LogWarning("Extraction failed or PDF is empty for CV ID: {CvId}", cvId);
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

                // Create a unique ID for each chunk
                Guid pointId = Guid.NewGuid();

                // Build the Qdrant point
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = pointId.ToString() },
                    Vectors = embeddingArray,
                    Payload =
                    {
                        // Store the actual text and CV ID in the payload so we can retrieve and filter by it later
                        ["cv_id"] = cvId,
                        ["chunk_index"] = i,
                        ["text"] = currentChunk
                    }
                };

                points.Add(point);
            }

            // Upsert (Insert or Update) the points into the Qdrant database
            await _qdrantClient.UpsertAsync(CollectionName, points);

            _logger.LogInformation("Successfully stored {ChunkCount} chunks in Qdrant for CV ID: {CvId}", chunks.Count, cvId);
        }

        private async Task EnsureCollectionExistsAsync()
        {
            bool collectionExists = await _qdrantClient.CollectionExistsAsync(CollectionName);

            if (!collectionExists)
            {
                _logger.LogInformation("Collection '{CollectionName}' does not exist. Creating new collection...", CollectionName);

                // nomic-embed-text outputs vectors with 768 dimensions
                await _qdrantClient.CreateCollectionAsync(
                    collectionName: CollectionName,
                    vectorsConfig: new VectorParams { Size = 768, Distance = Distance.Cosine }
                );
            }
        }

        public async Task<string> SearchCvAsync(string query, string cvId, int limit = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            _logger.LogInformation("Searching in Qdrant. CV ID: {CvId}, Query: '{Query}'", cvId, query);

            // 1. Convert the user's question into an embedding
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            var queryVector = queryEmbedding.ToArray();

            // 2. Create a filter to ONLY search within the specified CV ID
            // This solves the issue of fetching data from other uploaded CVs
            var filter = new Filter();
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "cv_id",
                    Match = new Match { Keyword = cvId } // Exact match on cv_id payload
                }
            });

            // 3. Search Qdrant for the closest matching vectors with the applied filter
            var searchResults = await _qdrantClient.SearchAsync(
                collectionName: CollectionName,
                vector: queryVector,
                filter: filter, // Apply the strict filter here
                limit: (ulong)limit
            );

            _logger.LogInformation("Found {ResultCount} matching chunks for CV ID: {CvId}", searchResults.Count, cvId);

            // 4. Extract the actual text from the retrieved payload
            var contextBuilder = new System.Text.StringBuilder();

            foreach (var result in searchResults)
            {
                // Retrieve the "text" field we saved earlier during upload
                if (result.Payload.TryGetValue("text", out var textValue))
                {
                    contextBuilder.AppendLine(textValue.StringValue);
                    contextBuilder.AppendLine("---"); // Separator between chunks for better LLM context reading
                }
            }

            return contextBuilder.ToString();
        }
    }
}