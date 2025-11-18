"""
Model Context Protocol server for the Proiect Senat RAG pipeline.

This MCP server exposes high-level tools that proxy the existing ASP.NET Core
endpoints so that any MCP-compatible client (e.g. Cursor, Claude Desktop, Windsurf)
can query the local Retrieval-Augmented Generation stack or call the
low-level LLM endpoint directly.

Usage:
    uv run mcp-server  # or `python -m mcp_server`

Configuration via environment variables:
    SENAT_API_BASE       Base URL of the ASP.NET backend (default http://localhost:5206)
    SENAT_API_SSL_VERIFY Set to "1"/"true" to enforce TLS verification.
    SENAT_DEFAULT_MODEL  Default LLM identifier (default llama3:latest)
"""

from __future__ import annotations

import os
from contextlib import asynccontextmanager
from typing import Any, Dict, Optional

import httpx
from mcp.server.fastmcp import FastMCP

API_BASE = os.environ.get("SENAT_API_BASE", "http://localhost:5206").rstrip("/")
DEFAULT_MODEL = os.environ.get("SENAT_DEFAULT_MODEL", "llama3:latest")
VERIFY_SSL = os.environ.get("SENAT_API_SSL_VERIFY", "false").lower() in {"1", "true", "yes"}

REQUEST_TIMEOUT = float(os.environ.get("SENAT_API_TIMEOUT", "120"))

server = FastMCP(
    "proiect-senat",
)


@asynccontextmanager
async def http_client():
    async with httpx.AsyncClient(
        base_url=API_BASE,
        timeout=httpx.Timeout(REQUEST_TIMEOUT),
        verify=VERIFY_SSL,
    ) as client:
        yield client


async def _post_json(path: str, payload: Dict[str, Any]) -> Dict[str, Any]:
    async with http_client() as client:
        response = await client.post(path, json=payload)
        response.raise_for_status()
        return response.json()


@server.tool(
    name="ask_senat",
    description="Query the Romanian Senate legal corpus with retrieval and LLM synthesis.",
)
async def ask_senat(
    question: str,
    model: Optional[str] = None,
    top_k: int = 5,
) -> Dict[str, Any]:
    """
    Runs the full RAG pipeline (embed -> Qdrant search -> LLM) exposed by /api/mcp/generate.
    Returns the generated answer along with the cited sources and latency breakdowns.
    """

    payload = {
        "query": question,
        "model": model or DEFAULT_MODEL,
        "topK": top_k,
    }

    data = await _post_json("/api/mcp/generate", payload)
    return data


@server.tool(
    name="llm_generate",
    description="Directly invoke the configured LLM without RAG context.",
)
async def llm_generate(
    prompt: str,
    model: Optional[str] = None,
    max_tokens: int = 512,
    temperature: float = 0.0,
) -> Dict[str, Any]:
    """
    Calls /api/tools/llm/generate to run free-form prompts against the selected model.
    """

    payload = {
        "prompt": prompt,
        "model": model or DEFAULT_MODEL,
        "max_tokens": max_tokens,
        "temperature": temperature,
    }

    data = await _post_json("/api/tools/llm/generate", payload)
    return data


@server.tool(
    name="count_documents",
    description="Count legal documents in the database, optionally filtered by year.",
)
async def count_documents(
    year: Optional[int] = None,
) -> Dict[str, Any]:
    """
    Counts documents in Qdrant. If year is provided, counts only documents from that year.
    Otherwise counts all documents.
    """

    payload = {}
    if year is not None:
        payload["year"] = year

    data = await _post_json("/api/tools/qdrant/count", payload)
    return data


def main() -> None:
    server.run()


if __name__ == "__main__":
    main()

