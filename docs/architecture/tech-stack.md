# Tech Stack

> Source: `docs/technical-analysis.md`, `docs/epic-backlog.md`
> Last Updated: 2026-09-24 (Revision 2)

---

## Runtime & Language

| Component | Choice | Notes |
|-----------|--------|-------|
| **Language** | Python 3.12 | Best-supported version across the ML libraries used (onnxruntime, umap-learn, audio-separator). Not 3.14: several deps lag behind |
| **Package Manager** | **uv** | Decided. `pyproject.toml` + `uv.lock`; `uv run` for every command |
| **CLI** | `typer` | Every pipeline stage is a CLI command (`musicclass ingest`, `embed`, `train`, `cluster`, `separate`, `report`) |
| **Platform** | Windows 11 | Primary dev environment. WSL2 used only for the one-off Essentia parity test |

---

## Data Locations

| What | Where | Notes |
|------|-------|-------|
| Audio library | `D:\Le musiqe\Tracks` | **Read-only** for every stage except the opt-in tag writer. Configured via `MUSICCLASS_LIBRARY_ROOT` |
| Runtime data (DB, embeddings, stems, reports) | Local non-synced dir, e.g. `D:\MusicClassData` | Configured via `MUSICCLASS_DATA_ROOT`. **Never inside OneDrive** (sync locks/corrupts SQLite and HDF5) |
| Models (`.onnx`, separation checkpoints) | `MUSICCLASS_DATA_ROOT\models` | Downloaded by a script; not in git |
| Code & docs | This repo (OneDrive) | Fine for source files |

---

## GPU & Inference

| Component | Choice | Notes |
|-----------|--------|-------|
| **GPU** | AMD Radeon RX 7600 (8GB VRAM, RDNA3 Navi 33, gfx1102) | Not in AMD's ROCm 7.2 support list (Windows or Linux) |
| **Embedding inference** | `onnxruntime-directml` | ONNX Runtime + DirectML — any DirectX 12 GPU |
| **Separation inference** | `audio-separator[dml]` (PyTorch + torch-directml, experimental) | CPU fallback via `audio-separator[cpu]` |
| **Fallback** | CPU | Embedding models are small enough for CPU |

> DirectML is in sustained engineering at Microsoft (successor: Windows ML). All ONNX sessions are created through a single `create_session()` wrapper so the execution provider can be swapped without touching pipeline code.

---

## ML Pipeline Libraries

### Audio I/O & Features

| Library | Purpose |
|---------|---------|
| `soundfile` + `soxr` | Decode (FLAC/WAV/AIFF) and high-quality resampling to 16 kHz |
| `audioread` / `ffmpeg` (via `librosa.load`) | MP3 decoding |
| `librosa` | Mel spectrogram (Essentia-compatible input for EffNet/MAEST) and low-level features: RMS, onset strength, spectral centroid/flux, band energies |

> `essentia` / `essentia-tensorflow` are **not** runtime dependencies — no Windows wheels. Essentia's **models** are used through their published ONNX files.

### Embedding Models (ONNX, from essentia.upf.edu/models)

| Model | File | Dim | Role |
|-------|------|-----|------|
| **Discogs-EffNet** | `discogs-effnet-bsdynamic-1.onnx` | 1280 | **Primary** embedding |
| Genre Discogs-400 head | `genre_discogs400-discogs-effnet-1` | 400 | Style activations as extra features |
| **MAEST** | `discogs-maest-*-*.onnx` (dynamic batch) | 768 | Comparison model |
| Mood / arousal-valence / danceability heads | Essentia classifier heads | small | Optional extra features (convert to ONNX or re-implement: they are tiny MLPs) |
| PANNs CNN14 | converted by `scripts/convert_panns.py` | 2048 | Optional lower baseline |

> Deferred (post-MVP): CLAP (music) / MuQ-MuLan for text search and zero-shot element tagging.

### Source Separation

| Library | Purpose |
|---------|---------|
| `audio-separator[dml]` | UVR wrapper. **Primary model:** Demucs `htdemucs_ft` (4 stems: vocals, drums, bass, other). **Comparison:** BS/Mel-RoFormer (PyTorch) |

### Classification, Clustering & Metrics

| Library | Purpose |
|---------|---------|
| `scikit-learn` | Label models (logistic regression, ridge, MLP), `StratifiedGroupKFold`, `HDBSCAN`, metrics (F1, AMI/ARI, silhouette), permutation importance |
| `umap-learn` | Dimensionality reduction for clustering and 2D visualization |
| `numpy`, `pandas` | Data handling |

> The standalone `hdbscan` package is no longer needed (HDBSCAN is in scikit-learn ≥ 1.3). DBCV can be computed with a small helper if required.

### Similarity Search

| Choice | When |
|--------|------|
| NumPy cosine (brute force) | Default — milliseconds for 10k × 1280 |
| `sqlite-vec` or `faiss-cpu` | Only if the library grows far beyond 10k tracks |

---

## Web App (MVP)

| Library | Purpose |
|---------|---------|
| `streamlit` | Experimentation UI: track table, filters, audio playback, review queue, run comparison, stem value report. **Reads results; does not run long pipeline jobs** |
| `plotly` | UMAP scatter plots, stem value charts, confusion matrices |

> Alternatives worth a look for pure exploration: Renumics Spotlight (embedding map + audio playback), marimo notebooks.
> Future (production): FastAPI + React (TanStack Table) + PostgreSQL/pgvector.

---

## Data & Storage

| Component | Choice | Purpose |
|-----------|--------|---------|
| **Database** | SQLite (`MUSICCLASS_DATA_ROOT\db\musicclass.db`) | Tracks, files, raw + parsed labels, runs, predictions, metrics |
| **ORM** | `SQLAlchemy` 2.x | Database access layer |
| **Embeddings** | `.npy` per track (per-window matrix) + pooled matrix per model/pooling (`.npy` or HDF5 via `h5py`) | Fast NumPy access |
| **Decoded audio cache** | 16 kHz mono `.npy`/FLAC per track | Decode MP3 once |
| **Stems** | Mono FLAC per track/stem | ~Half of WAV size, lossless |
| **Reports** | `predictions.csv`, `predictions.md`, `label_report.md` | First output format |
| **Metadata I/O** | `mutagen` | Read tags (all formats); write predicted code to a separate field (opt-in, later) |

---

## Supported Audio Formats

| Format | In library | Read audio | Read tags | Write predicted tags |
|--------|-----------|-----------|-----------|----------------------|
| MP3 | 3,903 | Yes | Yes (ID3) | Yes (ID3) |
| WAV | 310 | Yes | Yes (usually empty) | Yes (ID3 chunk) — verify DJ software reads it |
| AIFF | 32 | Yes | Yes (ID3) | Yes (ID3) |
| FLAC | 16 | Yes | Yes (Vorbis) | Yes (Vorbis) |

---

## Development Tools

| Tool | Purpose |
|------|---------|
| `pytest` | Unit and integration testing (label parser gets exhaustive unit tests) |
| `ruff` | Linting and formatting |
| `mypy` | Static type checking |
| `git` | Version control (repo to be initialized in EPIC-1) |

---

## Dependency Summary (`pyproject.toml`)

```toml
[project]
requires-python = ">=3.12,<3.13"
dependencies = [
  "onnxruntime-directml",
  "librosa", "soundfile", "soxr",
  "scikit-learn", "umap-learn", "numpy", "pandas",
  "SQLAlchemy", "mutagen", "h5py",
  "typer",
  "streamlit", "plotly",
]

[project.optional-dependencies]
separation = ["audio-separator[dml]"]   # installed for EPIC-4 only

[dependency-groups]
dev = ["pytest", "ruff", "mypy"]
```

---

## Constraints & Non-Negotiables

1. **The Genre field is never modified** — it holds the manual ground-truth labels.
2. **The audio library is read-only** except for the opt-in tag writer (which backs up original tags and supports dry-run).
3. **Runtime data outside OneDrive.**
4. **ONNX Runtime for embedding inference.** PyTorch is allowed **only** for source separation (audio-separator) and offline model conversion.
5. **DirectML is the GPU path** for now. No `HSA_OVERRIDE_GFX_VERSION` workarounds. Re-evaluate if ROCm adds gfx1102 or Windows ML offers a better AMD provider.
6. **SQLite for MVP** — no database servers during experimentation.
7. **Streamlit for MVP UI; pipeline jobs run from the CLI.**
8. **Mono stems, stored as FLAC.**
9. **Incremental processing** — every expensive stage skips tracks already processed.
10. **Grouped evaluation** — duplicates of the same audio never appear in both train and test.
