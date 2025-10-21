using System.Net.Http.Json;

namespace ProiectSenatCore.Embedding
{
    public class EmbeddingApiClient
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public static async Task<bool> EmbedBatchAsync(string inputDirectory)
        {
            string embeddingsPath = Path.Combine(Directories.BaseDirPath, "embeddings.json");
            if (File.Exists(embeddingsPath))
            {
                Console.WriteLine("Skipping embedding generation, embeddings.json already exists.");
                return false;
            }

            Console.WriteLine("Calling /embed-batch API...");

            var payload = new { input_dir = inputDirectory };
            try
            {
                var response = await HttpClient.PostAsJsonAsync("http://localhost:8000/embed-batch", payload);
                if (response.IsSuccessStatusCode)
                {
                    string responseText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("API response: " + responseText);
                    return true;
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API error ({response.StatusCode}): {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calling embedding API: " + ex.Message);
                return false;
            }
        }

        public static async Task<float[]?> EmbedAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("Cannot embed empty text.");
                return null;
            }

            var payload = new { text };
            try
            {
                var response = await HttpClient.PostAsJsonAsync("http://localhost:8000/embed", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();
                    if (result?.Embedding != null)
                    {
                        return result.Embedding;
                    }
                    else
                    {
                        Console.WriteLine("No embedding returned by API.");
                        return null;
                    }
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API error ({response.StatusCode}): {error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calling embedding API: " + ex.Message);
                return null;
            }
        }

        public class EmbedResponse
        {
            public float[]? Embedding { get; set; }
        }
    }
}