using Qdrant.Client.Grpc;
using Struct = Qdrant.Client.Grpc.Struct;

namespace ProiectSenatCore;

public class QdrantUploader
{
    private readonly QdrantGrpcClient _client;
    private readonly string _collectionName;
    private const int VectorSize = 384;

    public QdrantUploader(string host, int port, string collectionName)
    {
        _client = new QdrantGrpcClient(host, port);
        _collectionName = collectionName;
    }

    public async Task UploadPointsAsync(List<QdrantPoint> points)
    {
        // Validare
        foreach (var p in points)
        {
            if (p.Vector == null || p.Vector.Count != VectorSize)
                throw new Exception($"Invalid vector size for point id {p.Id}: {p.Vector?.Count ?? 0} (expected {VectorSize})");
        }

        Console.WriteLine("Uploading points to Qdrant...");
        var pointStructs = points.Select(p => {
            var vectors = new Vectors { Vector = new Vector() };
            vectors.Vector.Data.AddRange(p.Vector);

            var point = new PointStruct
            {
                Id = new PointId { Num = (ulong)p.Id },
                Vectors = vectors
            };

            var structPayload = ToStruct(p.Payload);
            foreach (var field in structPayload.Fields)
                point.Payload.Add(field.Key, field.Value);

            return point;
        }).ToList();

        var upsertRequest = new UpsertPoints
        {
            CollectionName = _collectionName,
            Points = { pointStructs }
        };

        var response = await _client.Points.UpsertAsync(upsertRequest);

        if (response != null && response.Result != null)
        {
            Console.WriteLine($"Uploaded {upsertRequest.Points.Count} points to Qdrant.");
            Console.WriteLine($"Result: {response.Result}");
            Console.WriteLine($"Time: {response.Time}");
            if (response.Usage != null)
            {
                Console.WriteLine($"Qdrant resource usage: {response.Usage}");
            }
        }
        else
        {
            Console.WriteLine("Failed to upload points to Qdrant (response or result was null).");
        }
    }

    private Struct ToStruct(Payload payload)
    {
        var s = new Struct();
        s.Fields.Add("text", new Value { StringValue = payload.Text });
        s.Fields.Add("an", new Value { IntegerValue = payload.An });
        s.Fields.Add("numar_lege", new Value { StringValue = payload.NumarLege });
        s.Fields.Add("cod_document", new Value { StringValue = payload.CodDocument });
        s.Fields.Add("filename", new Value { StringValue = payload.Filename });
        s.Fields.Add("chunk", new Value { IntegerValue = payload.Chunk });
        return s;
    }
}