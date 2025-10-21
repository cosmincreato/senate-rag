using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProiectSenatCore.Adapters
{
    // Adapter that calls your OllamaService.GenerateResponseAsync directly.
    // Lightweight concurrency limiting is included to avoid saturating a local LLM.
    public class OllamaAdapter : IModelAdapter
    {
        private readonly OllamaService _ollama;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public OllamaAdapter(OllamaService ollama)
        {
            _ollama = ollama ?? throw new ArgumentNullException(nameof(ollama));
        }

        public async Task<ModelResponse> GenerateAsync(string prompt, ModelOptions options, CancellationToken ct = default)
        {
            // Prevent too many concurrent local model calls (adjust concurrency for your machine)
            await _semaphore.WaitAsync(ct);
            try
            {
                // Call the concrete OllamaService method you provided
                var modelName = string.IsNullOrWhiteSpace(options.ModelName) ? "llama3:latest" : options.ModelName;
                var result = await _ollama.GenerateResponseAsync(prompt, modelName);

                if (result == null)
                {
                    return new ModelResponse("[Ollama returned null]", modelName, null, null, null);
                }

                // We don't have token counts from OllamaService; leave TokensIn/TokensOut null for now.
                return new ModelResponse(result, modelName, null, null, result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Surface error inline for a quick demo; replace with structured logging in production.
                return new ModelResponse($"[OllamaAdapter error] {ex.Message}", options.ModelName, null, null, null);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async IAsyncEnumerable<ModelResponse> StreamGenerateAsync(string prompt, ModelOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // Your OllamaService currently returns a single response (no streaming).
            // For demo, yield the single final response.
            var resp = await GenerateAsync(prompt, options, ct);
            yield return resp;
        }
    }
}