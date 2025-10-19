using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ProiectSenatCore;

public class OllamaService
{
    private static readonly HttpClient HttpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };
    private readonly string _baseUrl;

    public OllamaService(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl;
    }

    public async Task<string?> GenerateResponseAsync(string prompt, string model = "llama3:latest")
    {
        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false
            };

            var response = await HttpClient.PostAsJsonAsync($"{_baseUrl}/api/generate", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
                return result?.Response;
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Ollama API error ({response.StatusCode}): {error}");
                return $"Error: Unable to generate response. Status: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling Ollama API: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var response = await HttpClient.GetAsync($"{_baseUrl}/api/tags");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();
                return result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
            }
            else
            {
                Console.WriteLine($"Failed to fetch models: {response.StatusCode}");
                return new List<string>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching models: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<bool> IsModelAvailableAsync(string modelName)
    {
        var models = await GetAvailableModelsAsync();
        return models.Contains(modelName);
    }
}

public class OllamaGenerateRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "llama3:latest";

    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";

    [JsonPropertyName("stream")] public bool Stream { get; set; } = false;
}

public class OllamaGenerateResponse
{
    [JsonPropertyName("model")] public string? Model { get; set; }

    [JsonPropertyName("response")] public string? Response { get; set; }

    [JsonPropertyName("done")] public bool Done { get; set; }
}

public class OllamaTagsResponse
{
    [JsonPropertyName("models")] public List<OllamaModel>? Models { get; set; }
}

public class OllamaModel
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("digest")] public string Digest { get; set; } = "";

    [JsonPropertyName("modified_at")] public DateTime ModifiedAt { get; set; }
}