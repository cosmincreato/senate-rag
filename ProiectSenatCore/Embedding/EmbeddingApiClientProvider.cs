using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProiectSenatCore.Embedding
{
    // Adapter that implements IEmbeddingProvider by delegating to the existing
    // ProiectSenatCore.Embedding.EmbeddingApiClient static methods.
    // Because your EmbeddingApiClient currently exposes a single-text EmbedAsync,
    // this provider runs multiple single requests in parallel for batch calls.
    public class EmbeddingApiClientProvider : IEmbeddingProvider
    {
        private readonly int _parallelism;

        public EmbeddingApiClientProvider(int maxParallelism = 4)
        {
            _parallelism = Math.Max(1, maxParallelism);
        }

        public async Task<float[]> EmbedAsync(string text, string model = "all-MiniLM-L6-v2", CancellationToken ct = default)
        {
            // Delegate to your existing static client (namespace: ProiectSenatCore.Embedding)
            var vec = await ProiectSenatCore.Embedding.EmbeddingApiClient.EmbedAsync(text);
            if (vec == null) throw new InvalidOperationException("Embedding API returned null vector");
            return vec;
        }

        public async Task<List<float[]>> EmbedBatchAsync(IEnumerable<string> texts, string model = "all-MiniLM-L6-v2", CancellationToken ct = default)
        {
            var items = texts as string[] ?? texts.ToArray();
            var results = new float[items.Length][];
            var throttler = new SemaphoreSlim(_parallelism);

            var tasks = Enumerable.Range(0, items.Length).Select(async i =>
            {
                // allow cancellation to propagate
                await throttler.WaitAsync(ct);
                try
                {
                    var vec = await ProiectSenatCore.Embedding.EmbeddingApiClient.EmbedAsync(items[i]);
                    results[i] = vec ?? Array.Empty<float>();
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.Select(r => r ?? Array.Empty<float>()).ToList();
        }
    }
}