# Coding Standards

> Last Updated: 2026-09-24 (Revision 2 — aligned with technical-analysis.md Rev. 2)

---

## Python Version & Style

- **Python 3.12** (managed with uv) — use modern syntax (match/case, type union `X | Y`, etc.)
- **Formatter/Linter:** `ruff` — single tool for formatting, linting, and import sorting
- **Type Checker:** `mypy` in strict mode for new modules
- **Line Length:** 100 characters max
- **Quotes:** Double quotes for strings
- **Docstrings:** Google style, required for all public functions and classes

```python
def extract_embeddings(audio_path: str, model: str = "discogs_effnet") -> np.ndarray:
    """Extract per-window audio embeddings from a track using the specified model.

    Args:
        audio_path: Absolute path to the audio file.
        model: Model identifier. Defaults to Discogs-EffNet.

    Returns:
        Per-window embedding matrix of shape (n_windows, 1280).

    Raises:
        FileNotFoundError: If audio_path does not exist.
        ModelError: If the ONNX model fails to load.
    """
```

---

## Project Structure

```
MusicClass/                     # Repo (lives in OneDrive — code and docs only)
├── src/musicclass/
│   ├── cli.py                  # Typer entry point: ingest, embed, train, cluster, separate, report
│   ├── config.py               # Settings (env vars: MUSICCLASS_LIBRARY_ROOT, MUSICCLASS_DATA_ROOT)
│   ├── labels/                 # The DJ's label code
│   │   ├── schema.py               # Enums/dataclasses: Genre, Style, Element, SetCategory, LabelCode
│   │   ├── parser.py               # Genre-field string → LabelCode (+ malformed reasons)
│   │   └── formatter.py            # LabelCode → code string (for reports / tag writing)
│   ├── pipeline/
│   │   ├── ingest.py               # Stage 1: scan, audio hash, dedupe, read tags, parse labels, decode cache
│   │   ├── embedding.py            # Stage 2/5: ONNX embeddings (mix and stems), windowing, pooling
│   │   ├── features.py             # Low-level features (librosa)
│   │   ├── classify.py             # Stage 3: per-label models, grouped CV, predictions
│   │   ├── clustering.py           # UMAP + HDBSCAN, cluster metrics
│   │   ├── separation.py           # Stage 4: audio-separator wrapper
│   │   └── stem_value.py           # Stage 5: probes, ablation, fusion weights, element→stem check
│   ├── inference/
│   │   └── onnx.py                 # create_session() — the only place that picks execution providers
│   ├── db/
│   │   ├── models.py               # SQLAlchemy models
│   │   └── repository.py           # Data access functions
│   ├── export/
│   │   ├── report.py               # predictions.csv / predictions.md / label_report.md
│   │   └── metadata.py             # Opt-in tag writer (separate field, backup, dry-run)
│   └── ui/                     # Streamlit pages (read results, play audio, review queue)
│       ├── app.py
│       ├── track_browser.py
│       ├── cluster_viz.py
│       ├── review_queue.py
│       └── runs.py                 # Run comparison + stem value report
├── tests/
│   ├── unit/
│   ├── integration/
│   └── fixtures/               # Short audio clips (< 5 s) + label-string examples
├── scripts/                    # Model download, PANNs conversion, Essentia parity test (WSL2)
├── docs/
├── pyproject.toml
├── uv.lock
└── README.md

MUSICCLASS_DATA_ROOT (e.g. D:\MusicClassData — NOT in OneDrive, NOT in git)
├── db/musicclass.db
├── models/                     # .onnx files, separation checkpoints
├── audio16k/                   # Decoded 16 kHz mono cache, one file per track_id
├── embeddings/<model>/<source>/ # source = mix | drums | bass | other | vocals; per-window .npy
├── stems/<track_id>/           # drums.flac, bass.flac, other.flac, vocals.flac (mono)
├── reports/                    # predictions.csv, predictions.md, label_report.md
└── backups/                    # Original tag values before any write
```

> This is the target layout. Adjust as needed during implementation, but keep labels, pipeline stages, inference, database, export and UI separate.

---

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Files & modules | `snake_case` | `embedding.py`, `track_browser.py` |
| Classes | `PascalCase` | `TrackRepository`, `ClusteringPipeline` |
| Functions & methods | `snake_case` | `extract_features()`, `run_clustering()` |
| Constants | `UPPER_SNAKE_CASE` | `DEFAULT_SAMPLE_RATE`, `PANNS_EMBEDDING_DIM` |
| Private members | Leading underscore | `_load_model()`, `_cache_path` |
| Type aliases | `PascalCase` | `EmbeddingVector = np.ndarray` |

---

## Type Annotations

- **Required** on all function signatures (parameters and return types)
- **Required** on class attributes and module-level variables
- Use `from __future__ import annotations` for forward references
- Use `numpy.typing.NDArray` for typed NumPy arrays where practical

```python
from __future__ import annotations
import numpy as np
from numpy.typing import NDArray

def normalize_audio(audio: NDArray[np.float32], target_lufs: float = -14.0) -> NDArray[np.float32]:
    ...
```

---

## Error Handling

- **Use custom exception classes** for domain-specific errors, inheriting from a base `MusicClassError`
- **Fail fast** — raise exceptions at the point of failure, don't silently return None or empty results
- **Log errors with context** — include the track path, pipeline stage, and relevant parameters
- **Never catch bare `Exception`** unless re-raising or at a top-level boundary (CLI, UI handler)

```python
class MusicClassError(Exception):
    """Base exception for all MusicClass errors."""

class AudioLoadError(MusicClassError):
    """Failed to load or decode an audio file."""

class ModelInferenceError(MusicClassError):
    """ONNX model inference failed."""
```

---

## Pipeline Stage Contract

Every pipeline stage module must follow this pattern:

1. **Idempotent** — running a stage twice on the same track produces the same result
2. **Incremental** — check the database for processing status before doing work; skip already-processed tracks
3. **Cache results** — persist outputs (features, stems, embeddings) to the appropriate storage layer
4. **Report progress** — accept an optional callback or use logging to report progress during batch processing
5. **Configurable** — accept parameters via a config dict or dataclass, never hardcode model paths or thresholds

```python
from dataclasses import dataclass

@dataclass
class ClusteringConfig:
    """Configuration for the clustering pipeline stage."""
    umap_n_neighbors: int = 30
    umap_min_dist: float = 0.0
    umap_n_components: int = 15
    umap_metric: str = "cosine"
    hdbscan_min_cluster_size: int = 50
    hdbscan_min_samples: int = 10
    hdbscan_cluster_selection_method: str = "eom"

def run_clustering(
    embeddings: NDArray[np.float32],
    config: ClusteringConfig,
    progress_callback: Callable[[int, int], None] | None = None,
) -> ClusteringResult:
    ...
```

---

## Database Conventions

- **SQLAlchemy declarative models** — all table definitions in `src/db/models.py`
- **Repository pattern** — data access functions in `src/db/repository.py`, not scattered across pipeline code
- **Migrations** — for the MVP (SQLite), use a simple version table and manual schema updates. No Alembic unless complexity warrants it
- **Track identity** — `track_id` = SHA-256 of the **decoded audio samples** (not the path, not the raw file bytes). Path hashes break on move/rename and would count the ~700 duplicate copies in the library as different tracks; raw-file hashes change whenever tags are written. All file paths of a track live in a separate `track_files` table (one track → many files)
- **Labels** — store the **raw Genre string** exactly as read, plus the parsed fields (genre, style, intensity, 9 element levels, descriptors, label format, parse errors, held-out flag). Parsed fields are always re-derivable from the raw string by the parser
- **Label provenance** — every label row records its source: `genre_tag`, `review_ui`. Predictions live in a separate `predictions` table keyed by `(track_id, run_id)` — never mixed with manual labels
- **Processing status** — each pipeline stage has a status per track (`ingest_status`, `embedding_status`, `separation_status`) with values: `pending`, `processing`, `completed`, `failed`

---

## ONNX Inference Conventions

- **Single entry point:** all sessions are created by `musicclass.inference.onnx.create_session()` — no other module imports provider names (DirectML may later be replaced by a Windows ML provider)
- **Always specify providers explicitly:** `['DmlExecutionProvider', 'CPUExecutionProvider']`
- **Never assume GPU is available** — always include CPU as a fallback provider
- **Session reuse** — create ONNX sessions once and reuse across tracks (do not re-instantiate per track)
- **Batch when possible** — group tracks into batches appropriate for VRAM (profile to find optimal batch size)
- **Log which provider was selected** at session creation time

```python
import onnxruntime as ort
import logging

logger = logging.getLogger(__name__)

def create_session(model_path: str) -> ort.InferenceSession:
    """Create an ONNX inference session with GPU fallback to CPU."""
    providers = ["DmlExecutionProvider", "CPUExecutionProvider"]
    session = ort.InferenceSession(model_path, providers=providers)
    active_provider = session.get_providers()[0]
    logger.info("ONNX session created for %s using %s", model_path, active_provider)
    return session
```

---

## Testing

- **Framework:** `pytest`
- **Test location:** `tests/unit/` and `tests/integration/`
- **Naming:** test files mirror source files — `test_preprocessing.py` tests `preprocessing.py`
- **Unit tests** should not require GPU, audio files, or database — mock external dependencies
- **Integration tests** may use small audio fixtures (< 5s clips) stored in `tests/fixtures/`
- **Minimum coverage expectation:** all pipeline stage public functions must have unit tests
- **Use `pytest.mark.slow`** for tests that involve actual model inference or large file I/O

- **The label parser gets exhaustive unit tests** — every format in `technical-analysis.md` §2 (full, three-numbers-only, short format, plain text, empty, malformed) with real examples from the library, including keyboard-layout variants of the Shift symbols
- **Evaluation code must use grouped splits** (`StratifiedGroupKFold` with `track_id` groups) — add a test that fails if a duplicate track appears in both train and test

```python
@pytest.mark.slow
def test_effnet_embedding_extraction(sample_audio_path):
    """Integration test: verify Discogs-EffNet produces 1280-dim window embeddings."""
    windows = extract_embeddings(sample_audio_path)
    assert windows.ndim == 2 and windows.shape[1] == 1280


def test_parse_full_code():
    code = parse_label('2 2 3 1"#006000 groovy cong')
    assert (code.genre, code.style, code.intensity) == (Genre.PROGRESSIVE, Style.FLY_MELODIC, 3)
    assert code.elements[Element.PADS] == ElementLevel.INTENSE
    assert code.elements[Element.LOW_PADS] == ElementLevel.ABSENT
    assert code.descriptors == ("groovy", "cong")
```

---

## Logging

- Use Python's `logging` module — no `print()` statements in production code
- Log at appropriate levels:
  - `DEBUG` — per-track processing details, parameter values
  - `INFO` — stage start/complete, batch progress (e.g., "Processed 150/2000 tracks")
  - `WARNING` — recoverable issues (corrupt file skipped, fallback to CPU)
  - `ERROR` — failures that stop processing for a specific track
- Include structured context in log messages (track path, stage name, elapsed time)

---

## Configuration

- **Single config file** at `src/config.py` using a dataclass or Pydantic model
- **Environment variables** for paths that vary between machines: `MUSICCLASS_LIBRARY_ROOT` (default `D:\Le musiqe\Tracks`), `MUSICCLASS_DATA_ROOT` (e.g. `D:\MusicClassData`)
- **No hardcoded paths** — all file system paths come from config
- **Refuse to start** if `MUSICCLASS_DATA_ROOT` resolves inside a OneDrive folder
- **Library is read-only** — only `export/metadata.py` may open audio files for writing, and only when invoked with an explicit `--write` flag
- **Pipeline parameters** (UMAP, HDBSCAN settings) are stored per-run in the database, not in config — config only holds defaults

---

## Git Conventions

- **Branch naming:** `epic-N/short-description` (e.g., `epic-2/audio-preprocessing`)
- **Commit messages:** imperative mood, 72-char subject line, reference epic/story if applicable
  ```
  Add BPM and key extraction via Essentia

  Implements Stage 1 preprocessing using Essentia's RhythmExtractor
  and KeyExtractor. Results are cached in SQLite per track.

  Epic: EPIC-2
  ```
- **Do not commit:**
  - ONNX model files (large binaries — document download/conversion steps instead)
  - Audio files or stems
  - SQLite database files
  - HDF5 / `.npy` embedding caches, reports generated from the library
  - Virtual environment directories
