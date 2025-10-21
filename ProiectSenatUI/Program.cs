using System.Text.Json;
using System.Linq;
using ProiectSenatCore;
using ProiectSenatCore.Embedding;
using ProiectSenatCore.Adapters;

using ProiectSenatUI.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Minimal API + CORS + endpoint explorer for the new endpoints
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers(); // harmless for later if you add controllers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Existing service registrations (kept as you had them)
builder.Services.AddSingleton<OllamaService>();
builder.Services.AddSingleton<QdrantSearchService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ProiectSenatCore.Adapters.IModelAdapter, ProiectSenatCore.Adapters.OllamaAdapter>();

// Your embedding provider registration (keeps your existing namespace/implementation)
builder.Services.AddSingleton<ProiectSenatCore.Embedding.IEmbeddingProvider>(sp =>
{
    return new ProiectSenatCore.Embedding.EmbeddingApiClientProvider(maxParallelism: 4);
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

// Minimal API endpoints (Blazor host-friendly) -------------------------------------------------

// GET /api/tools - quick manifest
app.MapGet("/api/tools", () =>
{
    var manifest = new
    {
        tools = new object[]
        {
            new { name = "qdrant_search", id = "qdrant_search", endpoint = new { method = "POST", path = "/api/tools/qdrant/search" }, auth_scope = "vector:read" },
            new { name = "embedding_create", id = "embedding_create", endpoint = new { method = "POST", path = "/api/tools/embeddings" }, auth_scope = "embed:write" },
            new { name = "llm_generate", id = "llm_generate", endpoint = new { method = "POST", path = "/api/tools/llm/generate" }, auth_scope = "model:invoke" }
        }
    };
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

// POST /api/tools/embeddings
app.MapPost("/api/tools/embeddings", async (HttpRequest req, ProiectSenatCore.Embedding.IEmbeddingProvider embedding) =>
{
    var body = await req.ReadFromJsonAsync<JsonElement?>();

    if (body == null)
        return Results.BadRequest(new { error = "invalid or missing JSON body" });

    var json = body.Value;

    if (!json.TryGetProperty("texts", out var texts)) return Results.BadRequest(new { error = "texts required" });

    var list = new System.Collections.Generic.List<string>();
    foreach (var t in texts.EnumerateArray()) list.Add(t.GetString() ?? "");
    var model = json.TryGetProperty("model", out var m) ? m.GetString() ?? "" : "";
    var vecs = await embedding.EmbedBatchAsync(list, string.IsNullOrEmpty(model) ? "all-MiniLM-L6-v2" : model);
    return Results.Ok(new { model = model, vectors = vecs, dim = vecs.Count > 0 ? vecs[0].Length : 0 });
});

// POST /api/mcp/generate
app.MapPost("/api/mcp/generate", async (HttpRequest req,
                                         ProiectSenatCore.Embedding.IEmbeddingProvider embedding,
                                         ProiectSenatCore.Adapters.IModelAdapter modelAdapter,
                                         QdrantSearchService qdrant,
                                         ChatService chat) =>
{
    var body = await req.ReadFromJsonAsync<JsonElement?>();

    if (body == null)
        return Results.BadRequest(new { error = "invalid or missing JSON body" });

    var reqObj = body.Value;

    if (!reqObj.TryGetProperty("query", out var queryEl)) return Results.BadRequest(new { error = "query required" });
    var query = queryEl.GetString() ?? "";
    var topK = reqObj.TryGetProperty("topK", out var topKEl) ? topKEl.GetInt32() : 5;
    var model = reqObj.TryGetProperty("model", out var modelEl) ? modelEl.GetString() ?? "llama3:latest" : "llama3:latest";

    // 1) embed
    var qvec = await embedding.EmbedAsync(query);

    // 2) search
    var results = await qdrant.SearchSimilarTextsAsync(qvec.Select(f => (float)f).ToArray(), topK);

    // 3) build context
    var contextText = chat.BuildContextFromResults(results);

    // 4) prompt
    var prompt = $"System: You are a helpful assistant.\n\nContext:\n{contextText}\n\nUser: {query}\n\nAnswer:";

    // 5) call model
    var resp = await modelAdapter.GenerateAsync(prompt, new ModelOptions(model, 512, 0.0));

    // 6) return
    var sources = results.Select(r => new { r.LawNumber, r.LawCode, r.Score, r.Fn, r.Chunk }).Take(5);
    return Results.Ok(new { text = resp.Text, model = resp.Model, sources });
});

// optional: map controllers if you later add them
app.MapControllers();

app.Run();