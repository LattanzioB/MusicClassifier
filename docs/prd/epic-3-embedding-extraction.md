# EPIC-3: Embedding Extraction (Stages 2 & 5)

> **Priority:** P0
> **Estimated Effort:** Large
> **Phase:** Phase 1 (full mix) · Phase 3 (stems)
> **Dependencies:** EPIC-2 (decoded audio); EPIC-4 for per-stem embeddings
> **Can Start After:** EPIC-2
> **Sprint:** 2 (full mix) · 4 (stems)
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §8 (Rev. 2)

---

## Goal

Extract music-specific embeddings (Discogs-EffNet primary, MAEST comparison) from the full mix — and later from each stem — with windowing and structure-aware pooling.

---

## Scope

- `scripts/download_models.py`: EffNet ONNX, Discogs-400 head, MAEST ONNX
- Mel-spectrogram input in NumPy/librosa + **one-off parity test vs. Essentia in WSL2**
- Per-window embeddings per track: `embeddings/<model>/<source>/<track_id>.npy`
- Pooling strategies: mean · trimmed (skip first/last N%) · energy-weighted · mean+std
- Discogs-400 activations as extra features
- Low-level features: RMS, onset density, spectral flux/centroid, band energies, BPM
- Same code path for `source` ∈ {mix, drums, bass, other, vocals}
- Batched inference; GPU vs. CPU benchmark; incremental
- Optional: PANNs CNN14 → ONNX conversion as lower baseline

---

## Technical Context

### Why Discogs-EffNet (and not PANNs)

| Model | Dim | SR | ONNX | Trained on | Role |
|-------|-----|----|------|-----------|------|
| Discogs-EffNet | 1280 | 16 kHz | Published (`discogs-effnet-bsdynamic-1.onnx`) | Discogs 400 styles (incl. progressive/deep house, trance, techno) | **Primary** |
| MAEST | 768 | 16 kHz | Published (dynamic batch) | Discogs styles | Comparison |
| PANNs CNN14 | 2048 | 32 kHz | Convert yourself | AudioSet (general sounds) | Optional baseline |

Essentia's Python package has **no Windows wheels**, so the models are run directly with ONNX Runtime and the input spectrogram is re-implemented. The parity test (WSL2, `essentia-tensorflow`) confirms our embeddings match Essentia's (target cosine ≥ 0.99 on 10 tracks).

### Windowing & pooling

Progressive house tracks run 6–9 min with 1–2 min DJ intros/outros. Whole-track averages dilute character, so:

- keep the per-window matrix (~2 s patches)
- pool with a configurable trim (default: skip first/last 10%)
- compare pooling strategies by the EPIC-5 scores

### Inference

```python
session = create_session(models_dir / "discogs-effnet-bsdynamic-1.onnx")
patches = mel_patches(audio_16k)          # (n_windows, 128, 96)
embeddings = session.run(None, {input_name: patches})[1]   # (n_windows, 1280) — check output index
```

---

## Acceptance Criteria

- [ ] Parity test passes, or deviation documented with its effect on scores
- [ ] Full-mix EffNet embeddings for all unique tracks; runtime benchmarked and recorded
- [ ] Rerun skips existing embeddings
- [ ] Per-stem embeddings produced by the same command once stems exist

---

## Key Technical Decisions

- Patch hop / overlap and default trim percentage
- GPU vs. CPU default (by benchmark)
- Cache format for pooled matrices (`.npy` vs. HDF5)

---

## Downstream Epics

- **EPIC-5:** Classification & Clustering
- **EPIC-8:** Similarity
- **EPIC-10:** Stem Value Analysis (per-stem embeddings)
