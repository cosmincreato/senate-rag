# Proiect Senat

![alt text](https://i.imgur.com/5FPuxzi.png)

Proiect Senat is a Retrieval-Augmented Generation (RAG) platform developed during my Romanian Senate internship. It ingests legislative acts, builds dense embeddings, and exposes a private LLM experience that can answer questions with provenance. The stack combines .NET 8, Python, Qdrant, and Ollama so the entire workflow runs locally.

---

## Highlights

- **Legal-focused RAG pipeline:** cleans, chunks, and embeds Romanian Senate documents with [`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2).
- **Deterministic deployment:** Docker Compose brings up the ASP.NET backend, embedding server, MCP tools, and Ollama runtime, while Qdrant runs on Linux/WSL for storage reliability.
- **IDE-ready tooling:** FastMCP server exposes `ask_senat` and `llm_generate`, enabling direct consumption from Cursor, Claude Desktop, or Windsurf.
- **Privacy by design:** no cloud calls; every component runs offline, making it safe for sensitive legal corpora.

---

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/cosmincreato/proiectSenat.git
cd proiectSenat
```

### 2. Qdrant Vector Database Setup

**Recommended:** Use Docker to run Qdrant locally.

> **Important:** On Windows, you **must use WSL2 (Windows Subsystem for Linux)** for reliable Qdrant storage. Native Windows filesystems may cause corruption and zero-byte files.

#### 🪟 **Windows: How to Install WSL2 and Ubuntu**

1. **Open PowerShell as Administrator**

2. **Install WSL2 and Ubuntu:**

   ```powershell
   wsl --install
   ```

3. **After restart, open Ubuntu from the Start Menu.**
   - This opens a full Linux terminal.

---

#### **Linux/MacOS/WSL2: Setup Docker and Qdrant**

1. **Update and install Docker:**
   ```bash
   sudo apt update
   sudo apt install docker.io
   sudo usermod -aG docker $USER
   # Close and reopen your Ubuntu terminal so Docker permissions take effect
   ```

#### Start Qdrant Server

```bash
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

- **REST API:** [http://localhost:6333](http://localhost:6333)
- **gRPC API:** [http://localhost:6334](http://localhost:6334)

**Optional (persistent storage):**

```bash
docker run -p 6333:6333 -p 6334:6334 \
  -v $(pwd)/qdrant_storage:/qdrant/storage \
  qdrant/qdrant
```

#### Create a Collection

You can create a collection using:

```bash
python3 qdrant_collection.py
```

#### Populate DB

Run the Data Setup to let the Qdrant DB ingest the points.

### 3. Docker Setup

The entire project can be run using Docker Compose, which orchestrates all services (Ollama, embedding server, backend, and MCP server) in isolated containers. **Note:** Qdrant should be running separately (e.g., in WSL) and will be accessed via `host.docker.internal`.

### Prerequisites

- [Docker](https://www.docker.com/get-started) and [Docker Compose](https://docs.docker.com/compose/install/) installed
- At least 8GB of RAM (more recommended for LLM inference)
- Sufficient disk space for models and data

### Quick Start

1. **Start all services:**

   ```bash
   docker-compose up -d
   ```

This will:

- Pull and start Ollama LLM service
- Build and start the Python embedding server
- Build and start the ASP.NET Core backend
- Build and start the MCP server (optional)

**Note:** Make sure Qdrant is already running (e.g., in WSL) on ports 6333/6334. The backend will connect to it via `host.docker.internal`.

2. **Pull an Ollama model (required before first use):**

   ```bash
   docker exec -it proiect-senat-ollama ollama pull llama3:latest
   ```

3. **Access the services:**

   - **Backend UI:** http://localhost:5206
   - **Embedding API:** http://localhost:8000
   - **Qdrant REST API:** http://localhost:6333
   - **Ollama API:** http://localhost:11434

### Environment Variables

You can customize the Docker setup by creating a `.env` file in the project root:

```env
# Ollama configuration
OLLAMA_BASE_URL=http://ollama:11434

# Qdrant configuration (running separately in WSL)
QDRANT_HOST=host.docker.internal
QDRANT_PORT=6334
QDRANT_COLLECTION=proiect-senat

# Embedding API
EMBEDDING_API_URL=http://embed_server:8000

# Backend configuration
ASPNETCORE_ENVIRONMENT=Development
```

### Persistent Data

The following data is persisted in Docker volumes:

- **Ollama models:** Docker volume `ollama_data` (LLM models)
- **Application data:** `./ProiectSenatUI/input/`, `./ProiectSenatUI/output/`, etc.

**Note:** Qdrant storage is managed separately in your WSL environment.

### Building Individual Services

To rebuild a specific service after code changes:

```bash
# Rebuild and restart the backend
docker-compose up -d --build backend

# Rebuild and restart the embedding server
docker-compose up -d --build embed_server
```

### Troubleshooting

- **Qdrant connection issues:** Make sure Qdrant is running in WSL and accessible on ports 6333/6334. The backend connects via `host.docker.internal`.
- **Ollama model not found:** Make sure you've pulled a model: `docker exec -it proiect-senat-ollama ollama pull llama3:latest`
- **Port conflicts:** If ports are already in use, modify the port mappings in `docker-compose.yml`
- **Out of memory:** Increase Docker's memory limit in Docker Desktop settings
- **Service health checks failing:** Check logs with `docker-compose logs <service-name>`

---

## Run the MCP Server (optional)

If you want to chat with the Romanian Senate knowledge base straight from an MCP-compatible IDE (Cursor, Claude Desktop, Windsurf, etc.), spin up the included MCP server:

1. Make sure Python 3.14 is available locally, then create/activate an isolated environment (recommended):
   ```powershell
   py -3.14 -m venv .venv
   .\.venv\Scripts\activate
   ```
2. Install the MCP-specific dependencies (they are pinned to versions known to work on Python 3.14):
   ```powershell
   python -m pip install --upgrade pip
   python -m pip install -r requirements-mcp.txt
   ```
   > If you skip this step, you will see `ModuleNotFoundError: No module named "mcp"` when launching the server.
3. Make sure the ASP.NET backend is running (so `http://localhost:5206` is reachable).
4. Start the server:
   ```bash
   python mcp_server.py
   ```
5. Point your MCP client to the script. The default config assumes the backend runs on `http://localhost:5206`.

### Environment variables

| Variable               | Default                 | Description                                         |
| ---------------------- | ----------------------- | --------------------------------------------------- |
| `SENAT_API_BASE`       | `http://localhost:5206` | Base URL of the ASP.NET backend                     |
| `SENAT_API_SSL_VERIFY` | `false`                 | Set to `true` to enforce TLS certificate validation |
| `SENAT_DEFAULT_MODEL`  | `llama3:latest`         | Default model passed to the tooling endpoints       |
| `SENAT_API_TIMEOUT`    | `120`                   | HTTP timeout (seconds) for backend calls            |

The server exposes two MCP tools:

- `ask_senat` – runs the full RAG chain (`/api/mcp/generate`) and returns rich metadata (sources + timings).
- `llm_generate` – calls `/api/tools/llm/generate` for raw prompting without retrieval.
