import json

with open("ProiectSenatUI/embeddings.json", "r", encoding="utf-8") as f:
    data = json.load(f)

bad = []
for idx, d in enumerate(data):
    problems = []
    if 'id' not in d:
        problems.append("Missing 'id'")
    if 'vector' not in d:
        problems.append("Missing 'vector'")
    elif not isinstance(d['vector'], list):
        problems.append("'vector' is not a list")
    else:
        if len(d['vector']) != 384:
            problems.append(f"Wrong vector length: {len(d['vector'])}")
        non_numeric = [v for v in d['vector'] if not isinstance(v, (float, int))]
        if non_numeric:
            problems.append(f"Non-numeric elements in vector: {non_numeric[:5]}{'...' if len(non_numeric) > 5 else ''}")
    if problems:
        bad.append({'index': idx, 'id': d.get('id'), 'problems': problems})

print(f"Bad vectors: {len(bad)}")
for entry in bad:
    print(f"Index {entry['index']}, id: {entry['id']} - Problems: {entry['problems']}")