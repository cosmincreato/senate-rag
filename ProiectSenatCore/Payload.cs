namespace ProiectSenatCore;

using System.Text.Json.Serialization;

public class Payload
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("an")]
    public required int An { get; set; }

    [JsonPropertyName("numar_lege")]
    public required string NumarLege { get; set; }


    [JsonPropertyName("cod_document")]
    public required string CodDocument { get; set; }


    [JsonPropertyName("filename")]
    public required string Filename { get; set; }

    [JsonPropertyName("chunk")]
    public required int Chunk { get; set; }
}