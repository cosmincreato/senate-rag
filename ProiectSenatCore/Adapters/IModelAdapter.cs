namespace ProiectSenatCore.Adapters
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    // adapter interface for different LLM providers
    public record ModelOptions(string ModelName, int MaxTokens = 512, double Temperature = 0.0);

    public record ModelResponse(string Text, string Model, int? TokensIn = null, int? TokensOut = null, object? Raw = null);

    public interface IModelAdapter
    {
        Task<ModelResponse> GenerateAsync(string prompt, ModelOptions options, CancellationToken ct = default);
        IAsyncEnumerable<ModelResponse> StreamGenerateAsync(string prompt, ModelOptions options, CancellationToken ct = default);
    }
}