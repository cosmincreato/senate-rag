import os
import json
from fastapi import FastAPI, Request, HTTPException
from sentence_transformers import SentenceTransformer

app = FastAPI()

MODEL_NAME = 'sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2'
model = SentenceTransformer(MODEL_NAME)

@app.post("/embed")
async def embed(request: Request):
    data = await request.json()
    sentence = data.get("text")
    if not sentence:
        raise HTTPException(status_code=400, detail="No text provided")
    embedding = model.encode(sentence, show_progress_bar=False)
    return {"embedding": embedding.tolist()}

@app.post("/embed-batch")
async def embed_file(request: Request):
    data = await request.json()
    input_dir = data.get("input_dir")
    if not input_dir or not os.path.isdir(input_dir):
        raise HTTPException(status_code=400, detail=f"Directory not found: {input_dir}")

    sentence_entries = []
    for filename in os.listdir(input_dir):
        filepath = os.path.join(input_dir, filename)
        if not os.path.isfile(filepath):
            continue
        try:
            main, chunk_part = filename.split('_')
            year = main[:2]
            if year.startswith('9'):
                year = '19' + year
            else:
                year = '20' + year
            year = int(year)
            law = main[2:6].title() + "/" + str(year)
            code = main[6:]
            chunk_str = chunk_part.replace('chunk', '')
            chunk_num = int(os.path.splitext(chunk_str)[0])
        except Exception:
            continue

        with open(filepath, encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if line:
                    sentence_entries.append({
                        "text": line,
                        "an": year,
                        "numar_lege": law,
                        "cod_document": code,
                        "filename": main,
                        "chunk": chunk_num
                    })

    sentences = [entry['text'] for entry in sentence_entries]
    if not sentences:
        raise HTTPException(status_code=400, detail="No valid texts found in directory.")

    embeddings = model.encode(sentences, show_progress_bar=True)

    data = []
    for idx, (entry, emb) in enumerate(zip(sentence_entries, embeddings)):
        data.append({
            "id": idx,
            "vector": emb.tolist(),
            "payload": {
                "text": entry["text"],
                "an": entry["an"],
                "numar_lege": entry["numar_lege"],
                "cod_document": entry["cod_document"],
                "filename": entry["filename"],
                "chunk": entry["chunk"]
            }
        })

    output_file = os.path.join(os.path.dirname(input_dir), 'embeddings.json')
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

    return {"count": len(data), "embeddings_file": output_file}

@app.get("/")
def health():
    return {"status": "ok", "model": MODEL_NAME}