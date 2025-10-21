namespace ProiectSenatCore.Adapters
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    // Minimal normalized adapter interface used by the MCP controller/agent
    public record ModelOptions(string ModelName, int MaxTokens = 512, double Temperature = 0.0);

    public record ModelResponse(string Text, string Model, int? TokensIn = null, int? TokensOut = null, object? Raw = null);

    public interface IModelAdapter
    {
        /// <summary>
        /// Generate a completion for the provided prompt. Implementations should normalize the provider response.
        /// </summary>
        Task<ModelResponse> GenerateAsync(string prompt, ModelOptions options, CancellationToken ct = default);

        /// <summary>
        /// Optional streaming generation - implementations may throw NotSupportedException if streaming not available.
        /// For the demo you can leave it yielding a single final response.
        /// </summary>
        IAsyncEnumerable<ModelResponse> StreamGenerateAsync(string prompt, ModelOptions options, CancellationToken ct = default);
    }
}