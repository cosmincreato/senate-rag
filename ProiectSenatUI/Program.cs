using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using ProiectSenatCore;
using ProiectSenatCore.Embedding;
using ProiectSenatCore.Adapters;

using ProiectSenatUI.Components;
using ProiectSenatCore.Qdrant;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Minimal API + CORS + endpoint explorer for the new endpoints
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Configure Ollama service from environment
var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
builder.Services.AddSingleton<OllamaService>(sp => new OllamaService(ollamaBaseUrl));

// Configure Qdrant service from environment
var qdrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
var qdrantPort = int.TryParse(Environment.GetEnvironmentVariable("QDRANT_PORT"), out var port) ? port : 6334;
var qdrantCollection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "proiect-senat";
builder.Services.AddSingleton<QdrantSearchService>(sp => new QdrantSearchService(qdrantHost, qdrantPort, qdrantCollection));

builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ProiectSenatCore.Adapters.IModelAdapter, ProiectSenatCore.Adapters.OllamaAdapter>();
builder.Services.AddSingleton<ProiectSenatCore.Embedding.IEmbeddingProvider>(sp =>
{
    return new ProiectSenatCore.Embedding.EmbeddingApiClientProvider(maxParallelism: 4);
});

// Register a named HttpClient with a longer timeout for the UI to call the MCP API.
// Components should use IHttpClientFactory.CreateClient("ApiClient") to call the backend.
builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBase"] ?? "https://localhost:7027/");
    client.Timeout = TimeSpan.FromMinutes(30); // Increased for slow PCs
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// enable CORS for local dev so Blazor UI can call these endpoints
app.UseCors();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// GET /api/tools
app.MapGet("/api/tools", () =>
{
    var manifestPath = Path.Combine(Directories.BaseDirPath, "tools.json");

    if (!File.Exists(manifestPath))
        return Results.Problem("tools.json not found");

    var json = File.ReadAllText(manifestPath);
    var manifest = JsonSerializer.Deserialize<JsonElement>(json);
    return Results.Ok(manifest);
});

// POST /api/tools/qdrant/search
app.MapPost("/api/tools/qdrant/search", async (HttpRequest req, ProiectSenatCore.Embedding.IEmbeddingProvider embedding, QdrantSearchService qdrant) =>
{
    var body = await req.ReadFromJsonAsync<JsonElement?>();

    if (body == null)
        return Results.BadRequest(new { error = "invalid or missing JSON body" });

    var json = body.Value;

    if (!json.TryGetProperty("query_text", out var q) && !json.TryGetProperty("query_vector", out _))
        return Results.BadRequest(new { error = "query_text or query_vector required" });

    float[] queryVector;
    if (json.TryGetProperty("query_vector", out var qvec))
    {
        var list = new System.Collections.Generic.List<float>();
        foreach (var v in qvec.EnumerateArray()) list.Add(v.GetSingle());
        queryVector = list.ToArray();
    }
    else
    {
        var text = q.GetString() ?? "";
        queryVector = await embedding.EmbedAsync(text);
    }

    int topK = 5;
    if (json.TryGetProperty("top_k", out var t)) topK = t.GetInt32();

    var results = await qdrant.SearchSimilarTextsAsync(System.Array.ConvertAll(queryVector, f => (float)f), topK);
    return Results.Ok(new { items = results });
});

// POST api/tools/qdrant/count
// TODO: create this endpoint in order to count the number of laws from a year, etc. if needed
app.MapPost("/api/tools/qdrant/count", async (HttpRequest req, QdrantSearchService qdrant) =>
{
    try
    {
        var body = await req.ReadFromJsonAsync<JsonElement?>();

        int count = 0;

        if (body != null && body.Value.TryGetProperty("year", out var yearEl))
        {
            int year = yearEl.GetInt32();
            // Presupunem că QdrantSearchService are o metodă CountByYearAsync
            count = await qdrant.CountByYearAsync(year);
        }
        else
        {
            // Fără filtrare → număr total de documente
            count = await qdrant.CountAllAsync();
        }

        return Results.Ok(new { count });
    }
    catch (Exception ex)
    {
        return Results.Problem("Internal Server Error: " + ex.Message);
    }
});


// POST /api/tools/embeddings
app.MapPost("/api/tools/embeddings", async (HttpRequest req, ProiectSenatCore.Embedding.IEmbeddingProvider embedding, ILogger<Program> logger) =>
{
    try
    {
        var body = await req.ReadFromJsonAsync<JsonElement?>();
        if (body == null)
            return Results.BadRequest(new { error = "invalid or missing JSON body" });

        var json = body.Value;

        if (!json.TryGetProperty("texts", out var texts))
            return Results.BadRequest(new { error = "texts required" });

        var list = new System.Collections.Generic.List<string>();
        foreach (var t in texts.EnumerateArray())
            list.Add(t.GetString() ?? "");

        var model = json.TryGetProperty("model", out var m) ? m.GetString() ?? "" : "";

        var vecs = await embedding.EmbedBatchAsync(list, string.IsNullOrEmpty(model) ? "all-MiniLM-L6-v2" : model);

        return Results.Ok(new { model = model, vectors = vecs, dim = vecs.Count > 0 ? vecs[0].Length : 0 });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in /api/tools/embeddings");
        return Results.Problem("Internal Server Error: " + ex.Message);
    }
});

// POST /api/tools/llm/generate
// tool used for text generation based on a prompt
app.MapPost("/api/tools/llm/generate", async (HttpRequest req,
                                              ProiectSenatCore.Adapters.IModelAdapter modelAdapter) =>
{
    var body = await req.ReadFromJsonAsync<JsonElement?>();
    if (body == null)
        return Results.BadRequest(new { error = "invalid or missing JSON body" });
    var json = body.Value;
    if (!json.TryGetProperty("prompt", out var p)) return Results.BadRequest(new { error = "prompt required" });
    var prompt = p.GetString() ?? "";
    var model = json.TryGetProperty("model", out var m) ? m.GetString() ?? "llama3:latest" : "llama3:latest";
    var maxTokens = json.TryGetProperty("max_tokens", out var mt) ? mt.GetInt32() : 512;
    var temperature = json.TryGetProperty("temperature", out var temp) ? temp.GetDouble() : 0.0;
    var resp = await modelAdapter.GenerateAsync(prompt, new ModelOptions(model, maxTokens, temperature));
    return Results.Ok(new
    {
        text = resp.Text,
        model = resp.Model,
        tokens_in = resp.TokensIn,
        tokens_out = resp.TokensOut
    });
});

// Helper function to detect if a query is asking for a count
static bool IsCountQuery(string query)
{
    var lowerQuery = query.ToLowerInvariant();
    var countKeywords = new[] { "câte", "câți", "câteva", "cât", "număr", "numărul", "câte documente", "câte legi", "câte acte", "how many", "count", "number of", "total" };
    return countKeywords.Any(keyword => lowerQuery.Contains(keyword));
}

// Helper function to extract year from query if present
static int? ExtractYear(string query)
{
    var match = System.Text.RegularExpressions.Regex.Match(query, @"\b(19|20)\d{2}\b");
    if (match.Success && int.TryParse(match.Value, out var year))
        return year;
    return null;
}

// POST /api/mcp/generate
// Intelligently routes to counting or searching based on query type
app.MapPost("/api/mcp/generate", async (HttpRequest req,
                                         ProiectSenatCore.Embedding.IEmbeddingProvider embedding,
                                         ProiectSenatCore.Adapters.IModelAdapter modelAdapter,
                                         QdrantSearchService qdrant,
                                         ChatService chat,
                                         ILogger<Program> logger) =>
{
    var totalSw = Stopwatch.StartNew();
    var body = await req.ReadFromJsonAsync<JsonElement?>();

    if (body == null)
        return Results.BadRequest(new { error = "invalid or missing JSON body" });

    var reqObj = body.Value;

    if (!reqObj.TryGetProperty("query", out var queryEl)) return Results.BadRequest(new { error = "query required" });
    var query = queryEl.GetString() ?? "";
    var topK = reqObj.TryGetProperty("topK", out var topKEl) ? topKEl.GetInt32() : 5;
    var model = reqObj.TryGetProperty("model", out var modelEl) ? modelEl.GetString() ?? "llama3:latest" : "llama3:latest";

    try
    {
        // Check if this is a count query
        if (IsCountQuery(query))
        {
            var countSw = Stopwatch.StartNew();
            int? year = ExtractYear(query);
            int count = year.HasValue 
                ? await qdrant.CountByYearAsync(year.Value)
                : await qdrant.CountAllAsync();
            countSw.Stop();

            // Build a response with the count
            var countContext = year.HasValue
                ? $"În baza de date există {count} documente din anul {year.Value}."
                : $"În baza de date există {count} documente legale în total.";

            var countPrompt = $$"""
                Ești un asistent juridic specializat în documente legale românești din cadrul Senatului.
                Utilizatorul a întrebat: {{query}}
                
                Informație: {{countContext}}
                
                Răspunde în limba română dacă întrebarea este în română, altfel răspunde în engleză.
                Fii concis și direct. Nu menționa că ți s-a oferit context.
                """;

            var modelSw = Stopwatch.StartNew();
            var resp = await modelAdapter.GenerateAsync(countPrompt, new ModelOptions(model, 512, 0.0));
            modelSw.Stop();
            totalSw.Stop();

            logger.LogInformation("MCP count query timings: total={Total}ms count={Count}ms model={Model}ms",
                totalSw.ElapsedMilliseconds, countSw.ElapsedMilliseconds, modelSw.ElapsedMilliseconds);

            return Results.Ok(new
            {
                text = resp.Text,
                model = resp.Model,
                count = count,
                year = year,
                query_type = "count",
                sources = Array.Empty<object>(),
                timings = new
                {
                    total_ms = totalSw.ElapsedMilliseconds,
                    count_ms = countSw.ElapsedMilliseconds,
                    model_ms = modelSw.ElapsedMilliseconds
                }
            });
        }

        // Regular search query
        // 1) embed
        var embedSw = Stopwatch.StartNew();
        var qvec = await embedding.EmbedAsync(query);
        embedSw.Stop();

        // 2) search
        var searchSw = Stopwatch.StartNew();
        var results = await qdrant.SearchSimilarTextsAsync(qvec.Select(f => (float)f).ToArray(), topK);
        searchSw.Stop();

        // 3) build context
        var contextSw = Stopwatch.StartNew();
        var contextText = chat.BuildContextFromResults(results);
        contextSw.Stop();

        // 4) prompt
        var prompt = chat.BuildPromptWithContext(query, contextText);
        Console.WriteLine(prompt);

        // 5) call model
        var modelSw2 = Stopwatch.StartNew();
        var resp2 = await modelAdapter.GenerateAsync(prompt, new ModelOptions(model, 2048, 0.0)); // Increased max tokens
        modelSw2.Stop();

        totalSw.Stop();

        // 6) return with timings for debugging
        var sources = results.Select(r => new { r.LawNumber, r.LawCode, r.Score, r.Fn, r.Chunk }).Take(5);
        logger.LogInformation("MCP generate timings: total={Total}ms embed={Embed}ms search={Search}ms context={Context}ms model={Model}ms",
            totalSw.ElapsedMilliseconds, embedSw.ElapsedMilliseconds, searchSw.ElapsedMilliseconds, contextSw.ElapsedMilliseconds, modelSw2.ElapsedMilliseconds);

        return Results.Ok(new
        {
            text = resp2.Text,
            model = resp2.Model,
            sources,
            query_type = "search",
            timings = new
            {
                total_ms = totalSw.ElapsedMilliseconds,
                embed_ms = embedSw.ElapsedMilliseconds,
                search_ms = searchSw.ElapsedMilliseconds,
                context_ms = contextSw.ElapsedMilliseconds,
                model_ms = modelSw2.ElapsedMilliseconds
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in /api/mcp/generate for query: {Query}", query);
        return Results.Problem(title: "Generation error", detail: ex.Message);
    }
});

app.Run();