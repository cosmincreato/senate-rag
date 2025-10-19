namespace ProiectSenatCore;

using System.Text.Json.Serialization;

public class QdrantPoint
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("vector")]
    public required List<float> Vector { get; set; }

    [JsonPropertyName("payload")]
    public required Payload Payload { get; set; }
}