
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Task_corectev.core.Interfaces;
using Task_corectev.Infrastructure.Services;

namespace Task_corectev
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            // swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            #region ai
            // Setup Semantic Kernel to use Ollama via the local endpoint
            builder.Services.AddMemoryCache(); // add memory

            builder.Services.AddKernel()
                .AddOllamaChatCompletion(
                    modelId: "mistral:latest", // The model you pulled in Ollama
                    endpoint: new Uri("http://localhost:11434/v1"), // Default Ollama local endpoint
                    serviceId: "ChatModel"

                ).AddOllamaChatCompletion(
                modelId: "llama2:7b",
                endpoint: new Uri("http://localhost:11434"),
                serviceId: "SummaryModel"
            );
            // Add Qdrant Client (Default gRPC port is 6334)
            builder.Services.AddSingleton(new QdrantClient("localhost", 6334));

            // Add Ollama Text Embedding Generation (nomic-embed-text)
            builder.Services.AddKernel()
                // ... (Your previous Chat Model registrations)
                .AddOllamaTextEmbeddingGeneration(
                    modelId: "nomic-embed-text:latest",
                    endpoint: new Uri("http://localhost:11434")
                );

            // Register the services
            builder.Services.AddScoped<IPdfService, PdfService>();
            builder.Services.AddScoped<IRagService, RagService>();

            builder.Services.AddScoped<IChatService, ChatService>();
            // Register the Specialized Agents
            builder.Services.AddScoped<ISpecializedAgent, Task_corectev.Infrastructure.Agents.AtsAgent>();
            builder.Services.AddScoped<ISpecializedAgent, Task_corectev.Infrastructure.Agents.InterviewAgent>();
            #endregion

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Task API V1");
                    options.RoutePrefix = string.Empty;
                });
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
