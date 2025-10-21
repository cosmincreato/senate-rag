using ProiectSenatCore.Embedding;

namespace ProiectSenatCore;

public class ChatService
{
    private readonly OllamaService _ollamaService;
    private readonly QdrantSearchService _searchService;

    public ChatService(OllamaService ollamaService, QdrantSearchService searchService)
    {
        _ollamaService = ollamaService;
        _searchService = searchService;
    }

    public async Task<ChatResponse> ProcessUserQueryAsync(string userQuery, string model = "llama3:latest")
    {
        var response = new ChatResponse
        {
            UserQuery = userQuery,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var queryEmbedding = await EmbeddingApiClient.EmbedAsync(userQuery);
            if (queryEmbedding == null)
            {
                response.BotResponse =
                    "Nu am putut procesa intrebarea ta.";
                response.IsError = true;
                return response;
            }

            // Cautam documente relevante in Qdrant
            var searchResults =
                await _searchService.SearchSimilarTextsAsync(queryEmbedding, limit: 5);

            if (!searchResults.Any())
            {
                response.BotResponse =
                    "Nu am gasit niciun raspuns relevant pentru aceasta intrebare.";
                response.RelevantDocuments = new List<SearchResult>();
                return response;
            }

            // Construim contextul din rezultatele cautarii
            var context = BuildContextFromResults(searchResults);
            response.RelevantDocuments = searchResults;

            // Cream prompt-ul pentru LLM
            var prompt = BuildPromptWithContext(userQuery, context);
            Console.WriteLine(prompt);

            // Generam raspunsul folosind Ollama
            var llmResponse = await _ollamaService.GenerateResponseAsync(prompt, model);

            if (string.IsNullOrEmpty(llmResponse))
            {
                response.BotResponse =
                    "Nu am putut obtine un raspuns din cauza unei erori.";
                response.IsError = true;
            }
            else if (llmResponse.StartsWith("Error:"))
            {
                response.BotResponse = "Nu am putut obtine un raspuns din cauza unei erori.";
                response.IsError = true;
            }
            else
            {
                response.BotResponse = llmResponse;
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ChatService.ProcessUserQueryAsync: {ex.Message}");
            response.BotResponse = "O eroare a aparut in timpul procesarii intrebarii tale.";
            response.IsError = true;
            return response;
        }
    }

    public string BuildContextFromResults(List<SearchResult> results)
    {
        if (!results.Any()) return "";

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Pasajele gasite in documente:");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            contextBuilder.AppendLine($"\n{i + 1}. Legea {result.LawNumber} Doc. {result.LawCode} (Anul {result.Year}):");
            contextBuilder.AppendLine(result.Text.Trim());
        }

        return contextBuilder.ToString();
    }

    private string BuildPromptWithContext(string userQuery, string context)
    {
        return $$"""
                 Ești un asistent juridic specializat în documente legale românești din cadrul Senatului.
                 Folosește exclusiv contextul furnizat din documentele legale pentru a răspunde la întrebarea utilizatorului cât mai precis și clar.

                 Context:
                 {{context}}

                 Întrebarea utilizatorului: {{userQuery}}

                 Instrucțiuni:
                 - Răspunde în limba română dacă întrebarea este în română, altfel răspunde în engleză
                 - Bazează răspunsul strict pe contextul furnizat, dar nu menționa faptul că ți s-a oferit contextul
                 - Dacă contextul nu conține suficiente informații pentru a răspunde la întrebare, menționează acest lucru clar
                 - Citează numerele și anii legilor relevante atunci când faci referire la dispoziții specifice
                 - Fii concis și la obiect în răspunsurile tale
                 - Răspunde cât mai natural, fără a menționa faptul că răspunzi la o întrebare oferită de un utilizator
                 """;
    }

    public async Task<bool> TestServicesAsync()
    {
        try
        {
            // Test Qdrant connection
            var qdrantOk = await _searchService.TestConnectionAsync();

            // Test embedding service
            var testEmbedding = await EmbeddingApiClient.EmbedAsync("test");
            var embeddingOk = testEmbedding != null;

            // Test Ollama availability
            var models = await _ollamaService.GetAvailableModelsAsync();
            var ollamaOk = models.Any();

            Console.WriteLine($"Service Status - Qdrant: {qdrantOk}, Embedding: {embeddingOk}, Ollama: {ollamaOk}");

            return qdrantOk && embeddingOk && ollamaOk;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error testing services: {ex.Message}");
            return false;
        }
    }
}

public class ChatResponse
{
    public string UserQuery { get; set; } = "";
    public string BotResponse { get; set; } = "";
    public List<SearchResult> RelevantDocuments { get; set; } = new List<SearchResult>();
    public DateTime Timestamp { get; set; }
    public bool IsError { get; set; } = false;
}