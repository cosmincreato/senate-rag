from qdrant_client import QdrantClient
from qdrant_client.models import VectorParams

client = QdrantClient("localhost", port=6333)

collection_name = "proiect-senat"
vector_size = 384

client.recreate_collection(
    collection_name=collection_name,
    vectors_config=VectorParams(size=vector_size, distance="Cosine")
)
print(f"Created or reset collection '{collection_name}' with dimension {vector_size}")