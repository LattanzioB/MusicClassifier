# EPIC-1: Project Foundation & Development Environment

> **Priority:** P0 — Must be first
> **Estimated Effort:** Small
> **Phase:** Setup
> **Dependencies:** None
> **Can Start After:** Immediately
> **Sprint:** 1
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) (Rev. 2)

---

## Goal

Set up the repository, Python project, configuration, data locations, database, and verify GPU inference.

---

## Scope

- `git init`; `.gitignore` for data, models, audio, DB, caches, `.venv`
- uv project (`pyproject.toml`, Python 3.12, `uv.lock`); ruff, mypy, pytest configured
- Typer CLI skeleton (`uv run musicclass --help`)
- Config via env vars:
  - `MUSICCLASS_LIBRARY_ROOT` — default `D:\Le musiqe\Tracks` (read-only)
  - `MUSICCLASS_DATA_ROOT` — e.g. `D:\MusicClassData`; startup check **refuses paths inside OneDrive**
- SQLite schema v1: `tracks`, `track_files`, `labels`, `runs`, `predictions`, `metrics`, per-stage status
- `inference/onnx.py::create_session()` + `musicclass gpu-check` smoke test
- Logging setup
- Clean `.claude/settings.local.json` (contains npm/Next/Supabase/psql permissions and a `C:\Users\BrunoLattanzio\...` path copied from another project)

---

## Technical Context

### Hardware

- **GPU:** AMD Radeon RX 7600 (8GB VRAM, RDNA3 Navi 33, **gfx1102**)
- **ROCm:** not officially supported for this card as of ROCm 7.2 (Windows and Linux)
- **Solution:** ONNX Runtime + DirectML for embeddings; PyTorch + DirectML (audio-separator, experimental) for separation. DirectML is in sustained engineering (successor: Windows ML) → keep provider selection inside `create_session()` only.

### GPU check

```python
import logging
import onnxruntime as ort

logger = logging.getLogger(__name__)

def create_session(model_path: str) -> ort.InferenceSession:
    providers = ["DmlExecutionProvider", "CPUExecutionProvider"]
    session = ort.InferenceSession(model_path, providers=providers)
    logger.info("ONNX session for %s using %s", model_path, session.get_providers()[0])
    return session
```

### Schema sketch

| Table | Key columns |
|-------|-------------|
| `tracks` | `track_id` (audio hash), artist, title, bpm, key, duration, stage statuses |
| `track_files` | path, `track_id`, size, mtime, format |
| `labels` | `track_id`, source (`genre_tag` / `review_ui`), raw string, format, genre, style, intensity, element levels ×9, descriptors, parse_error, held_out flag |
| `runs` | run_id, kind, config JSON, git commit, timestamp |
| `predictions` | `track_id`, run_id, field, value, confidence |
| `metrics` | run_id, field, metric, value, fold |

---

## Acceptance Criteria

- [ ] `uv sync` installs cleanly on Windows 11
- [ ] `uv run musicclass gpu-check` reports `DmlExecutionProvider` active on the RX 7600
- [ ] DB is created under `MUSICCLASS_DATA_ROOT`; a OneDrive path is rejected with a clear error
- [ ] Repository under git with a correct `.gitignore`

---

## Key Technical Decisions (resolved)

- uv + Python 3.12
- Track identity = hash of decoded audio; one track → many files
- Runtime data outside OneDrive

---

## Downstream Epics

- **EPIC-2:** Library Ingestion & Label Parsing
- **EPIC-4:** Source Separation (can start its benchmark independently)
