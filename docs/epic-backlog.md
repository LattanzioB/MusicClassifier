# Epic Backlog: Progressive House Classification & Recommendation System

> Derived from: `docs/technical-analysis.md` (Revision 2)
> Status: Draft — Revision 2
> Last Updated: 2026-09-24

---

## Overview

This backlog decomposes the system into implementable epics. Revision 2 reorders the work around one fact: **the library already contains ~2,100 tracks hand-labeled with the DJ's personal code** (genre, style, intensity, 9 sound elements, descriptors — see technical-analysis §2). The system first learns and reproduces that code from the full mix, and then — as the final goal — measures **which separated stem carries the most value** for each part of the code.

### Guiding Principles

- **Experimentation-first:** the MVP is a workbench. Every experiment gets a score against the manual labels.
- **Full mix first, stems last:** stems are only valuable if they beat the full-mix baseline.
- **Staged, cached, incremental pipeline:** each stage is a CLI command that skips finished work.
- **Library is sacred:** read-only; the Genre field is never modified; outputs go to reports first.
- **GPU-aware, not GPU-bound:** ONNX + DirectML for embeddings (CPU viable); PyTorch + DirectML for separation.

---

## Epic Map

```
EPIC-1: Project Foundation
   │
   └──► EPIC-2: Library Ingestion & Label Parsing          ← Phase 0
           │
           └──► EPIC-3: Embedding Extraction (full mix)    ← Phase 1
                   │
                   ├──► EPIC-5: Classification & Clustering        ← Phase 1-2
                   │       │
                   │       ├──► EPIC-7: Prediction Report & Tag Export  (report: Phase 1, tags: Phase 4)
                   │       │
                   │       └──► EPIC-9: Experiment Tracking & Validation
                   │
                   ├──► EPIC-8: Similarity & Recommendation (post-MVP)
                   │
                   └──► EPIC-4: Source Separation              ← Phase 3
                           │
                           └──► EPIC-3 (per-stem embeddings)
                                   │
                                   └──► EPIC-10: Stem Value Analysis  ← Phase 3 (FINAL GOAL)

EPIC-6: Streamlit UI (parallel track after EPIC-2; grows with each phase)
```

---

## EPIC-1: Project Foundation & Development Environment

**Priority:** P0 — Must be first
**Estimated Effort:** Small
**Dependencies:** None

### Goal
Set up the repository, Python project, configuration, data locations, database, and verify GPU inference.

### Scope
- `git init`; `.gitignore` for data, models, audio, DB, caches, `.venv`
- uv project (`pyproject.toml`, Python 3.12, `uv.lock`); ruff, mypy, pytest configured
- Typer CLI skeleton (`musicclass --help`)
- Config via env vars: `MUSICCLASS_LIBRARY_ROOT` (default `D:\Le musiqe\Tracks`), `MUSICCLASS_DATA_ROOT` (outside OneDrive; startup check refuses OneDrive paths)
- SQLite schema v1: `tracks`, `track_files`, `labels`, `runs`, `predictions`, `metrics`, stage status columns
- `create_session()` ONNX wrapper + GPU smoke test (DirectML provider active; CPU fallback works)
- Logging setup
- Clean `.claude/settings.local.json` (entries copied from another project: npm/Next/Supabase/psql)

### Acceptance Criteria
- [ ] `uv sync` installs everything cleanly on Windows 11
- [ ] `uv run musicclass gpu-check` reports `DmlExecutionProvider` active on the RX 7600
- [ ] SQLite DB is created under `MUSICCLASS_DATA_ROOT`; a OneDrive path is rejected with a clear error
- [ ] Repository is under git with a correct `.gitignore`

### Key Technical Decisions (resolved)
- uv (not pip/Poetry); Python 3.12
- Audio-content hash as track identity; one-to-many `track_files`

---

## EPIC-2: Library Ingestion & Label Parsing (Stage 1)

**Priority:** P0
**Estimated Effort:** Medium
**Dependencies:** EPIC-1

### Goal
Register every track in the library, deduplicate, read existing tags, and turn the DJ's label code into structured ground truth — producing a label report that drives all later decisions.

### Scope
- Scan `D:\Le musiqe\Tracks` for MP3 / WAV / AIFF / FLAC (≈4,270 files)
- Compute `track_id` = hash of decoded audio; group duplicate files (~1,240 files share a name with another)
- Read tags with mutagen: artist, title, BPM, key, Genre (raw string), comment, grouping
- **Label parser** (`labels/parser.py`) for all formats:
  - Full: `G S I EEEEEEEEE descriptors` — position-based element decoding (`0` absent, digit present, symbol intense)
  - Three numbers only: `G S I`
  - Short format: `C I descriptors` — intensity and descriptors used; the set-building group C is ignored
  - Plain-text store genres → ignored as labels; empty → unlabeled
  - Malformed → recorded with a reason, never guessed
- **Held-out test set:** tracks in unclear folders (`000Deep`, `06Minimal`, `haus`, `Remolinos`, `Best2024`, `LemAdDy`, `classifiedNew`) are excluded from training and evaluation, and kept to try the algorithm on later
- Descriptor normalization and grouping (structure / character / sound / artist style) per the glossary in technical-analysis §2.2
- Conflict handling: duplicate files with different codes → flagged in the report
- Decode + resample to 16 kHz mono cache (for EPIC-3)
- **`label_report.md`**: counts per field/value, format breakdown, malformed entries (path + string), duplicate groups, conflicts, tracks missing BPM/key
- Incremental: only new/changed files are processed on rerun

### Acceptance Criteria
- [ ] All ≈4,270 files registered; duplicates collapse to unique `track_id`s
- [ ] Parser unit tests cover every format with real examples; full-format parse success ≥ 99% of full-format strings
- [ ] Label report generated and reviewed by the DJ; malformed entries listed with file paths for manual fixing
- [ ] Held-out folder tracks flagged and excluded from training/evaluation
- [ ] Rerun on an unchanged library finishes in seconds–minutes and changes nothing
- [ ] The library is not modified (verified by file mtimes/hashes in an integration test)

### Key Technical Decisions
- Minimum examples per class to be trainable (proposal: 20 for classes, 30 for descriptors)
- Any rare descriptor not in the glossary is listed in the label report for confirmation

---

## EPIC-3: Embedding Extraction (Stages 2 & 5)

**Priority:** P0
**Estimated Effort:** Large
**Dependencies:** EPIC-2 (decoded audio); EPIC-4 for per-stem embeddings

### Goal
Extract music-specific embeddings (Discogs-EffNet primary, MAEST comparison) from the full mix — and later from each stem — with windowing and structure-aware pooling.

### Scope
- Model download script (EffNet ONNX, Discogs-400 head, MAEST ONNX)
- Mel-spectrogram input re-implemented (16 kHz, 96 mel bands, ~2 s patches) + **one-off parity test vs. Essentia in WSL2** (embedding cosine similarity ≥ 0.99)
- Per-window embeddings saved per track (`embeddings/<model>/<source>/<track_id>.npy`)
- Pooling strategies: mean, trimmed (skip first/last N%), energy-weighted, mean+std — pooled matrices per strategy
- Discogs-400 style activations stored as extra features
- Low-level features (RMS, onset density, spectral flux/centroid, band energies, BPM)
- Same code path for stems (`source` ∈ mix, drums, bass, other, vocals)
- Batch inference, benchmark GPU vs. CPU, incremental processing
- Optional: PANNs CNN14 conversion as a lower baseline

### Acceptance Criteria
- [ ] Parity test passes (or documented deviation with its effect on scores)
- [ ] Full-mix EffNet embeddings for all unique tracks; runtime benchmarked and recorded
- [ ] Rerun skips existing embeddings
- [ ] Per-stem embeddings produced by the same command once stems exist

### Key Technical Decisions
- Window hop / patch overlap; default trim percentage
- GPU vs. CPU as default (decided by benchmark)

---

## EPIC-4: Source Separation Pipeline (Stage 4)

**Priority:** P1 — needed for the final goal (Phase 3)
**Estimated Effort:** Large
**Dependencies:** EPIC-1, EPIC-2

### Goal
Split every track into drums, bass, other and vocals stems, reliably and resumably, within 8 GB VRAM.

### Scope
- `audio-separator[dml]` integration (optional dependency group `separation`)
- **Benchmark on 20 tracks**: `htdemucs_ft` vs. a RoFormer chain (vocals RoFormer → Demucs on instrumental); DirectML vs. CPU; speed, VRAM, listening check
- Segment-based processing to fit 8 GB VRAM
- Mono FLAC output: `stems/<track_id>/{drums,bass,other,vocals}.flac`
- Quality check: stems sum ≈ original mix (residual energy threshold)
- Resumable batch runs (overnight), per-track status, failures logged and retried
- Option to separate only a representative section (e.g. central 3 minutes) if full-track time is prohibitive — decided from the benchmark

### Acceptance Criteria
- [ ] Benchmark report with chosen model and measured s/track
- [ ] Full library separated without VRAM crashes; interrupted runs resume where they stopped
- [ ] Residual check passes for ≥ 98% of tracks; failures listed

### Key Technical Decisions
- Default model (htdemucs_ft vs. RoFormer chain) — by benchmark
- Full track vs. representative section

---

## EPIC-5: Classification & Clustering Engine (Stage 3)

**Priority:** P0
**Estimated Effort:** Large
**Dependencies:** EPIC-3

### Goal
Learn the DJ's code from the embeddings — one model per label field — with honest grouped cross-validation, and use clustering for discovery and mislabel detection.

### Scope

#### Supervised label models
| Field | Task |
|-------|------|
| Genre (deep / progressive / techno / trance) | Multi-class, balanced class weights |
| Style (deep / fly-melodic / groovy / hype) | Multi-class |
| Intensity (1–4) | Ordinal regression (+ low-level features) |
| Elements 1–6, 9 | Per element: absent / present / intense (ordinal or two binaries) |
| Elements 7, 8 | Not trained until ≥ 30 positives — listed as "needs labels" |
| Descriptors | One binary model per frequent word; structural ones (`ctd`, `hungup`, `bigdrop`) use per-window features |

- `StratifiedGroupKFold` (5 folds, groups = `track_id`) — duplicates never leak
- Metrics per field: macro-F1, per-class recall, confusion matrix; MAE/Spearman for intensity
- Per-track confidence; disagreements with manual labels flagged as possible mislabels
- Predictions for unlabeled / partially labeled tracks, assembled into the DJ's code format
- Feature sets as configurations: EffNet / MAEST / + Discogs-400 / + low-level; pooling strategy

#### Clustering (discovery)
- UMAP + scikit-learn HDBSCAN; configurable, saved per run
- AMI/ARI of clusters vs. each label field; silhouette, DBCV
- 2D UMAP for visualization

### Acceptance Criteria
- [ ] Baseline scores recorded for every label field with grouped CV
- [ ] Predictions + confidence stored per run for every track
- [ ] Training + CV for all fields runs in < 2 minutes
- [ ] Clustering runs, parameters and metrics saved and comparable

### Key Technical Decisions
- Ordinal vs. two-binary formulation for element levels (decide by score)
- Confidence threshold below which a prediction is reported as "uncertain"

---

## EPIC-6: Streamlit Experimentation UI

**Priority:** P0 (parallel track — starts after EPIC-2)
**Estimated Effort:** Large
**Dependencies:** EPIC-1, EPIC-2; grows with EPIC-3, 5, 9, 10

### Goal
A workbench to browse the library by the DJ's code, listen, review predictions, compare runs, and see the stem value report. Pipeline jobs are run from the CLI; the UI reads their results.

### Scope
- **Track browser:** artist, title, BPM, key, manual code, predicted code, confidence, cluster; filters on every label field (incl. elements and descriptors); inline audio snippet (from the main section, not the intro)
- **Cluster view:** 2D UMAP colored by any label field or cluster; hover info; select to play
- **Review queue:** least-confident and disagreeing tracks first; accept / correct → saved as `review_ui` labels
- **Label health:** counts per class, rare classes needing examples
- **Runs:** compare metrics between runs (per field)
- **Stem value report** (after EPIC-10)

### Acceptance Criteria
- [ ] Table filters by every label field and plays audio inline
- [ ] Review queue saves corrections as new labels (never touching audio files)
- [ ] Cluster scatter plot renders for ~3,500 tracks and is interactive
- [ ] Runs can be compared side by side

### Key Technical Decisions
- `st.dataframe` vs. AgGrid for the table
- Snippet strategy: on-the-fly slice of the decoded cache vs. pre-rendered previews

---

## EPIC-7: Prediction Report & Tag Export

**Priority:** P0 for the report (Phase 1); P2 for tag writing (Phase 4)
**Dependencies:** EPIC-5

### Goal
Deliver predictions to the DJ — first as files, later (opt-in) inside each track's metadata in a separate field.

### Scope

#### 7a — Report (first deliverable)
- `predictions.csv`: path, artist, title, predicted code, per-field confidence, manual code, agreement
- `predictions.md`: grouped by genre/style; uncertain and disagreeing tracks highlighted
- Code formatter reproduces the DJ's format exactly (`2 2 3 1"#006000 groovy cong`)

#### 7b — Tag writing (later, opt-in)
- Write predicted code to a **separate** field — visible (`Grouping` or `Comment`, prefixed `AI:`) + machine-readable (`TXXX:MC_CODE`, `MC_CONFIDENCE`, `MC_VERSION` / Vorbis equivalents)
- **Never modify `Genre`**
- Dry-run by default; `--write` required; backup of original values in `MUSICCLASS_DATA_ROOT/backups`; rollback command
- MP3, AIFF, FLAC, WAV
- Verify display/sort in Traktor and Rekordbox

### Acceptance Criteria
- [ ] Report files generated after every training run
- [ ] Formatter round-trips: `format(parse(s)) == s` for all well-formed manual codes
- [ ] (7b) Tag writer changes only the target fields; Genre and all other tags byte-identical; rollback restores originals
- [ ] (7b) Predicted field visible and sortable in the DJ software

### Key Technical Decisions
- Visible field: Grouping vs. Comment (test in both DJ apps)
- Whether reviewed predictions may be promoted into Genre (manual, per track)

---

## EPIC-8: Similarity & Recommendation Engine

**Priority:** P2 — Post-MVP
**Estimated Effort:** Medium
**Dependencies:** EPIC-3 (EPIC-10 weights improve it)

### Goal
"Find similar tracks" using embeddings — optionally weighted by stem importance and filtered by the DJ's code.

### Scope
- Cosine similarity with NumPy (brute force); sqlite-vec / FAISS only if scale requires
- Filters: "similar to X, same style, intensity +1", "similar congas"
- Stem-weighted similarity using EPIC-10 weights (e.g. "similar drums")
- "Find similar" in the UI with playback
- Post-MVP: CLAP / MuQ-MuLan text search

### Acceptance Criteria
- [ ] Top-10 similar tracks judged coherent by listening on 20 query tracks
- [ ] Query < 1 s for 10,000 tracks

---

## EPIC-9: Experiment Tracking & Validation

**Priority:** P1
**Estimated Effort:** Medium
**Dependencies:** EPIC-5 (EPIC-6 for display)

### Goal
Make every experiment comparable: same folds, same metrics, logged runs, and qualitative tools for listening checks.

### Scope
- Run log in SQLite: config (model, pooling, features, params), metrics per field, timestamp, git commit
- Fixed fold assignment stored once (grouped, stratified) so every run uses the same splits
- Comparison table/charts between runs
- Unseen-folder test: predictions for the held-out folders reviewed by the DJ
- Listening tools: cluster-center sampling, boundary sampling, mislabel candidates
- Learning curves (score vs. number of labels) — shows where more labels help most
- Optional: MLflow if the custom log becomes limiting

### Acceptance Criteria
- [ ] Every CLI training/clustering run is logged with config and metrics
- [ ] Two runs can be compared per label field in one view
- [ ] Learning curves available for each field

---

## EPIC-10: Stem Value Analysis — FINAL GOAL

**Priority:** P1 (the project's final goal; starts once EPIC-4 and per-stem EPIC-3 are done)
**Estimated Effort:** Large
**Dependencies:** EPIC-3 (per-stem), EPIC-4, EPIC-5, EPIC-9

### Goal
Cluster each stem and determine, **with evidence and per label field**, which stem (drums, bass, other, vocals) carries the most value for the classification — then use that to weight the final model.

### Scope
- **Per-stem clustering:** UMAP + HDBSCAN per stem across the whole library; cross-tab with label fields; listening sessions to name stem sub-groups
- **Five measurements** (same grouped folds, vs. full-mix baseline):
  1. Single-stem probe per label field
  2. Leave-one-stem-out ablation
  3. Learned fusion weights (stacking) + grouped permutation importance
  4. Cluster agreement (AMI/ARI) per stem per field
  5. Element → expected-stem check (bass/low pads → bass; congas/hi-hats → drums; pads/acid/minimal → other; vocals → vocals)
- **Stem weight vector per label field**; final model = mix + weighted stems where they improve scores
- Stem value report (Markdown + UI page)

### Acceptance Criteria
- [ ] Stem value table per label field with confidence intervals across folds
- [ ] Final stem-weighted model compared with the full-mix model per field; kept only where it helps
- [ ] Stem clusters listened to and named (at least drums, bass, other)
- [ ] Report explains which stem matters most for each part of the code

---

## Epic Priority Summary

| Epic | Priority | Phase | Can Start After |
|------|----------|-------|-----------------|
| EPIC-1: Project Foundation | P0 | Setup | Immediately |
| EPIC-2: Library Ingestion & Label Parsing | P0 | Phase 0 | EPIC-1 |
| EPIC-3: Embedding Extraction | P0 | Phase 1 (stems: Phase 3) | EPIC-2 |
| EPIC-5: Classification & Clustering | P0 | Phase 1-2 | EPIC-3 |
| EPIC-7a: Prediction Report | P0 | Phase 1 | EPIC-5 |
| EPIC-6: Streamlit UI | P0 | Parallel | EPIC-2 |
| EPIC-9: Experiment Tracking & Validation | P1 | Phase 1-2 | EPIC-5 |
| EPIC-4: Source Separation | P1 | Phase 3 | EPIC-2 |
| EPIC-10: Stem Value Analysis | P1 (final goal) | Phase 3 | EPIC-3 (stems), EPIC-4, EPIC-5 |
| EPIC-7b: Tag Writing | P2 | Phase 4 | EPIC-5 (ideally EPIC-10) |
| EPIC-8: Similarity & Recommendation | P2 | Post-MVP | EPIC-3 |

---

## Suggested Implementation Order

### Sprint 1: Foundation + Labels
- **EPIC-1** — full
- **EPIC-2** — full (label report reviewed with the DJ; malformed entries fixed)

### Sprint 2: Full-Mix Baseline + First Report
- **EPIC-3** — full-mix embeddings (EffNet), parity test
- **EPIC-5** — label models + grouped CV; clustering
- **EPIC-7a** — `predictions.csv` / `predictions.md`
- **EPIC-9** — run log + fixed folds

### Sprint 3: Workbench & Review Loop
- **EPIC-6** — browser, cluster view, review queue, run comparison
- **EPIC-3/5** — MAEST comparison, pooling experiments
- **EPIC-4** — separation benchmark on 20 tracks (starts the long batch run in the background)

### Sprint 4: Stems — Final Goal
- **EPIC-4** — full library separation
- **EPIC-3** — per-stem embeddings
- **EPIC-10** — stem clustering + stem value analysis → weighted final model
- **EPIC-6** — stem value report page

### Sprint 5: Delivery
- **EPIC-7b** — opt-in tag writing
- **EPIC-8** — similarity (optional)

---

## Resolved Decisions

1. **Ground truth** — the DJ's code in the Genre field, with descriptor glossary (technical-analysis §2). Set category is **not** predicted; unclear folders are a held-out test set.
2. **Genre field is never modified.** First output is a report file; later a separate metadata field.
3. **Supervised first** — one model per label field; clustering for discovery.
4. **Embeddings** — Discogs-EffNet primary, MAEST comparison, PANNs optional baseline.
5. **Full mix before stems** — stem value is measured against the full-mix baseline (EPIC-10).
6. **Mono stems, FLAC.** Disk space unconstrained, but FLAC costs nothing.
7. **Energy curve deferred** — context-dependent (depends on preceding tracks).
8. **CLAP deferred** — post-MVP (text search, zero-shot element tagging).
9. **Tooling** — uv, Python 3.12, Typer CLI, git.
10. **Track identity** — hash of decoded audio; grouped CV.

## Open Questions for the DJ

None at the moment. Rare descriptors not yet in the glossary will be listed in the EPIC-2 label report.
