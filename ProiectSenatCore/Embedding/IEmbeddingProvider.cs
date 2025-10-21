namespace ProiectSenatCore.Embedding
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal embedding provider contract used by the MCP controllers and services.
    /// Implementations can call a local FastAPI embedder, a cloud provider, or wrap
    /// other existing helper classes.
    /// </summary>
    public interface IEmbeddingProvider
    {
        Task<float[]> EmbedAsync(string text, string model = "all-MiniLM-L6-v2", CancellationToken ct = default);
        Task<List<float[]>> EmbedBatchAsync(IEnumerable<string> texts, string model = "all-MiniLM-L6-v2", CancellationToken ct = default);
    }
}