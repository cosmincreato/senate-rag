using Qdrant.Client.Grpc;

namespace ProiectSenatCore;

public class QdrantSearchService
{
    private readonly QdrantGrpcClient _client;
    private readonly string _collectionName;

    public QdrantSearchService(string host = "localhost", int port = 6334, string collectionName = "proiect-senat")
    {
        _client = new QdrantGrpcClient(host, port);
        _collectionName = collectionName;
    }

    public async Task<List<SearchResult>> SearchSimilarTextsAsync(float[] queryVector, int limit = 5)
    {
        try
        {
            var searchRequest = new SearchPoints
            {
                CollectionName = _collectionName,
                Limit = (ulong)limit,
                WithPayload = new WithPayloadSelector { Enable = true }
            };

            searchRequest.Vector.AddRange(queryVector);

            var response = await _client.Points.SearchAsync(searchRequest);

            var results = new List<SearchResult>();
            if (response?.Result != null)
            {
                foreach (var point in response.Result)
                {
                    var searchResult = new SearchResult
                    {
                        Score = point.Score,
                        Text = ExtractStringFromPayload(point.Payload, "text"),
                        Year = ExtractIntFromPayload(point.Payload, "an"),
                        LawNumber = ExtractStringFromPayload(point.Payload, "numar_lege"),
                        LawCode = ExtractStringFromPayload(point.Payload, "cod_document"),
                        Fn = ExtractStringFromPayload(point.Payload, "filename"),
                        Chunk = ExtractIntFromPayload(point.Payload, "chunk")
                    };
                    results.Add(searchResult);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching Qdrant: {ex.Message}");
            return new List<SearchResult>();
        }
    }

    private string ExtractStringFromPayload(IDictionary<string, Value> payload, string key)
    {
        if (payload.TryGetValue(key, out var value) && value.StringValue != null)
        {
            return value.StringValue;
        }

        return "";
    }

    private int ExtractIntFromPayload(IDictionary<string, Value> payload, string key)
    {
        if (payload.TryGetValue(key, out var value))
        {
            return (int)value.IntegerValue;
        }

        return 0;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var healthRequest = new HealthCheckRequest();
            var response = await _client.Qdrant.HealthCheckAsync(healthRequest);
            return response != null && response.Title == "qdrant - vector search engine";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Qdrant connection test failed: {ex.Message}");
            return false;
        }
    }
}

public class SearchResult
{
    public float Score { get; set; }
    public string Text { get; set; } = "";
    public int Year { get; set; }
    public string LawNumber { get; set; } = "";
    public string LawCode { get; set; } = "";
    public string Fn { get; set; } = "";
    public int Chunk { get; set; }
    public string Filename { get; set; } = "";

    public override string ToString()
    {
        return $"[{Year}, {Fn}, Score: {Score:F3}] {Text}";
    }
}