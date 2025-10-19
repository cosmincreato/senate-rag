namespace ProiectSenatCore;

using System.Text.Json;

public class PointService
{
    public static List<QdrantPoint> LoadPoints(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<QdrantPoint>>(json);
    }
}