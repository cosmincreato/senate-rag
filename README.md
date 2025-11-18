# Senate RAG (Proiect Senat)

![Project banner](https://i.imgur.com/5FPuxzi.png)

Senate RAG is a retrieval‑augmented generation (RAG) stack purpose‑built for Romanian legislative documents. It ingests Senate acts, generates multilingual dense embeddings, stores them in a Vector Database, and serves answers through a local LLM with full privacy. Everything runs on your machine via Docker, no cloud calls required.

Built originally during an internship with the Romanian Senate, the project is now packaged for repeatable, production‑grade deployments with health checks, isolated services, and clear operational boundaries.

---

## Why it’s useful

- Answers questions about Romanian legal documents with cited sources
- Fully offline by design (Ollama, embeddings, vector DB, and API all local)
- Multilingual embeddings for robust Romanian text handling

---

## Architecture

The stack is orchestrated via Docker Compose:

- Ollama (LLM runtime)
- Embedding API (FastAPI + Sentence Transformers)
- ASP.NET Core backend and UI
- MCP server (Model Context Protocol) for IDE integration
- Qdrant (vector database) runs separately (recommended on Linux or WSL2 on Windows)

Key ports:

- Backend UI/API: http://localhost:5206
- Embedding API: http://localhost:8000
- Ollama API: http://localhost:11434
- Qdrant REST: http://localhost:6333 (external)
- Qdrant gRPC: http://localhost:6334 (external)

---

## Features

- Legal‑focused RAG pipeline

  - Embeddings model: `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` (384‑dim)
  - Vector store: Qdrant with cosine distance
  - File‑name–to‑metadata inference tailored to Romanian legal corpora

- Deterministic deployment

  - Dockerized services with health checks and explicit dependencies
  - Persistent volumes for models and data
  - Environment‑driven configuration

- Privacy by default

  - All components run locally; no external network calls are required

- IDE‑ready (MCP)
  - Ask questions and get answers with citations via MCP tools:
    - ask_senat → full RAG chain
    - llm_generate → direct LLM prompt (no retrieval)
    - count_documents → Qdrant counts (with optional year filter)

---

## What’s in the repo

- `docker-compose.yml` – Orchestrates Ollama, Embedding API, Backend, MCP server
- `Dockerfile.backend` – Builds the ASP.NET Core backend
- `Dockerfile.embed_server` – Builds the FastAPI embedding service
- `Dockerfile.mcp_server` – Builds the MCP server image
- `embed_server.py` – Embedding API (FastAPI) with:
  - `POST /embed` (single text)
  - `POST /embed-batch` (directory ingestion → writes `embeddings.json`)
  - `GET /` (health + model info)
- `mcp_server.py` – Exposes MCP tools that proxy backend endpoints:
  - `/api/mcp/generate`, `/api/tools/llm/generate`, `/api/tools/qdrant/count`
- `qdrant_collection.py` – Creates/resets a Qdrant collection (`proiect-senat`, 384‑dim)
- `ProiectSenatCore/`, `ProiectSenatUI/` – ASP.NET Core application and data directories (mounted into the backend container:
  - `input/`, `output/`, `chunked_output/`, `tessdata/`, `tools.json`, `embeddings.json`)

---

## Production‑readiness at a glance

- Containerized services with explicit health checks and startup ordering
- Separate vector database process (recommended on Linux/WSL2 for reliable storage)
- No hidden state in containers; state lives in:
  - Qdrant (external service)
  - Mounted volumes (e.g., `embeddings.json`, input/output folders)
- Pinned Python dependencies for the MCP server for repeatable builds
- Clear, environment‑driven configuration

---

## Quick start

1. Start Qdrant (recommended on Linux/WSL2)

On Windows, use WSL2 (native Windows FS can cause corruption/zero‑byte files with Qdrant).

```bash
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

Optional persistent storage:

```bash
docker run -p 6333:6333 -p 6334:6334 \
  -v $(pwd)/qdrant_storage:/qdrant/storage \
  qdrant/qdrant
```

Create/reset the collection (dimension 384 for MiniLM):

```bash
python3 qdrant_collection.py
```

2. Bring up the stack (Ollama, Embedding API, Backend, MCP)

```bash
docker-compose up -d
```

Pull an Ollama model (once):

```bash
docker exec -it proiect-senat-ollama ollama pull llama3:latest
```

3. Prepare and embed your documents

- Place your pre‑chunked text files into: `./ProiectSenatUI/chunked_output/`
- File names should encode metadata (the embedding server infers year, law number, code, chunk), for example:
  - `<YY><LLLL><CODE>_chunk<N>.txt`
  - The service infers:
    - `an` (year) from `YY` (19xx if `YY` starts with “9”, else 20xx)
    - `numar_lege`, `cod_document`, `chunk`, and `filename`

Generate embeddings:

```bash
curl -X POST http://localhost:8000/embed-batch \
  -H "Content-Type: application/json" \
  -d '{"input_dir": "/app/chunked_output"}'
```

This writes `embeddings.json` next to the input directory (already volume‑mounted into the backend container).

4. Ingest into Qdrant

If your backend workflow handles ingestion automatically from `embeddings.json`, you’re done.
Otherwise, here’s a minimal ingestion example you can run locally:

```python
# ingest_embeddings.py
import json
from qdrant_client import QdrantClient
from qdrant_client.models import PointStruct

client = QdrantClient(host="localhost", port=6333)
collection = "proiect-senat"

with open("ProiectSenatUI/embeddings.json", encoding="utf-8") as f:
    data = json.load(f)

points = [
    PointStruct(id=entry["id"], vector=entry["vector"], payload=entry["payload"])
    for entry in data
]

client.upsert(collection_name=collection, points=points)
print(f"Upserted {len(points)} points.")
```

```bash
python3 ingest_embeddings.py
```

5. Ask questions

- Backend UI/API: http://localhost:5206
- MCP (IDE integration): run the MCP container (already started by Compose) or run locally, then call `ask_senat`.

Example MCP tool call (conceptual):

```
ask_senat(question="Care este procedura pentru ...?", top_k=5)
```

---

## Configuration

Create a `.env` file in the project root to override defaults:

```env
# Ollama
OLLAMA_BASE_URL=http://ollama:11434

# Qdrant (external)
QDRANT_HOST=host.docker.internal
QDRANT_PORT=6334
QDRANT_COLLECTION=proiect-senat

# Embedding API
EMBEDDING_API_URL=http://embed_server:8000

# Backend
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:5206

# MCP server
SENAT_API_BASE=http://backend:5206
SENAT_DEFAULT_MODEL=llama3:latest
SENAT_API_SSL_VERIFY=false
SENAT_API_TIMEOUT=120
```

---

## Embedding API details

- Model: `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` (384‑dim)
- Endpoints:
  - `POST /embed` → `{ "text": "..." }` → `{ "embedding": [...] }`
  - `POST /embed-batch` → `{ "input_dir": "/path/in/container" }` → writes `embeddings.json`
  - `GET /` → health and model info

Returned payload format (per item) in `embeddings.json`:

```json
{
  "id": 0,
  "vector": [ ... 384 floats ... ],
  "payload": {
    "text": "…",
    "an": 2024,
    "numar_lege": "Lege/2024",
    "cod_document": "XYZ",
    "filename": "241234XYZ",
    "chunk": 3
  }
}
```

---

## MCP tools

The MCP server proxies the backend’s RAG and utility endpoints:

- `ask_senat(question, model="llama3:latest", top_k=5)` → `/api/mcp/generate`
- `llm_generate(prompt, model="llama3:latest", max_tokens=512, temperature=0.0)` → `/api/tools/llm/generate`
- `count_documents(year=None)` → `/api/tools/qdrant/count`

Use it from MCP‑compatible tools/IDEs (Cursor, Claude Desktop, Windsurf, etc.).

---

## Troubleshooting

- Qdrant on Windows: Use WSL2 + Linux filesystem
- Model missing: `docker exec -it proiect-senat-ollama ollama pull llama3:latest`
- Health checks: `docker-compose ps` and `docker-compose logs <service>`
- Port conflicts: adjust in `docker-compose.yml`
- Memory pressure: increase Docker Desktop resource limits (8 GB+ recommended)
