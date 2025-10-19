# Proiect Senat

This project is developed as part of my internship at the Romanian Senate. It is a modern Retrieval-Augmented Generation pipeline designed to process and embed large legal corpora, enabling semantic search and contextually-aware responses with Large Language Models.

![alt text](https://i.imgur.com/5FPuxzi.png)
---

## Features

- **Text Chunking & Embedding**: Preprocesses and splits large documents, then maps sentences & paragraphs to a 384 dimensional dense vector space using the [`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2) model.
- **Embeddings Storage**: Stores embeddings and associated text segments in `embeddings.json` for downstream retrieval.
- **RAG Workflow**: Enables retrieval of relevant text segments in response to user queries, providing augmented prompts to LLMs for improved answers.
- **Cross-platform UI**: Modern Blazor web interface for easy interaction with the data processing pipeline.
- **Dual Interface**: Both console application and web UI available for different use cases.
- **Modular Architecture**: Core business logic separated into reusable library components.
- **Ollama Integration**: Easily connect to local LLMs via Ollama for fully private, offline inference.

---

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/cosmincreato/proiectSenat.git
cd proiectSenat
```

### 2. Python Environment Setup

- **Python version:** 3.8 or newer

```bash
python3 -m venv venv
source venv/bin/activate
```

**Install dependencies:**

```bash
pip install -r requirements.txt
```

---

### 3. Start the Embedding Server

You need to run the embedding server to generate vector representations for your documents.

**Start the server:**

```bash
uvicorn embed_server:app --host 0.0.0.0 --port 8000
```
- The server will be available at [http://localhost:8000](http://localhost:8000)
- Make sure the server stays running while you use the application.

---

### 4. .NET Environment Setup

- **.NET version:** .NET 6.0 or newer
- Restore NuGet packages if prompted.

---

### 5. Qdrant Vector Database Setup

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

---

## Ollama Integration

Ollama lets you run LLMs like Llama 2, Mistral, Phi, or CodeLlama locally on your computer—no cloud or API keys needed.

### 1. Install Ollama

- [Download Ollama](https://ollama.com/download) for Windows, macOS, or Linux.
- Follow the installation instructions for your platform.

### 2. Run a Model

For example, to run Llama 3:

```bash
ollama pull llama3:latest
ollama run llama3:latest
```

The default Ollama server runs at `http://localhost:11434`