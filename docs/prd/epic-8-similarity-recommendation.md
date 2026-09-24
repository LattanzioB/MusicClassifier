# EPIC-8: Similarity & Recommendation Engine

> **Priority:** P2 — Post-MVP
> **Estimated Effort:** Medium
> **Phase:** Post-MVP
> **Dependencies:** EPIC-3 (EPIC-10 weights improve it)
> **Can Start After:** EPIC-3
> **Sprint:** 5 (optional)
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) (Rev. 2)

---

## Goal

"Find similar tracks" using embeddings — optionally weighted by stem importance and filtered by the DJ's code.

---

## Scope

- Cosine similarity on pooled embeddings with NumPy (brute force)
- Filters combined with similarity: "similar to X, same style, intensity +1", "has congas"
- Stem-weighted similarity using EPIC-10 weights, including single-stem queries ("similar drums", "similar bassline")
- "Find similar" in the UI with playback
- Post-MVP: CLAP / MuQ-MuLan natural-language search

---

## Technical Context

- 10,000 × 1280 float32 ≈ 50 MB; a brute-force cosine query takes milliseconds — no index needed
- `sqlite-vec` or `faiss-cpu` only if the library grows far beyond 10k tracks
- Stem-weighted vector: concatenation of L2-normalized per-source embeddings × weights

---

## Acceptance Criteria

- [ ] Top-10 similar tracks judged coherent by listening for 20 query tracks
- [ ] Query < 1 s for 10,000 tracks
- [ ] Filters and stem-weighted mode work together

---

## Key Technical Decisions

- Pooling used for similarity (may differ from classification)
- Default stem weights for "overall" similarity (from EPIC-10)
