# Progressive House Classification & Recommendation System

> Last Updated: 2026-09-24 (Revision 2)
> See [Revision History](#14-revision-history) for what changed from the original analysis.

## 1. Problem Statement

DJs face a practical challenge: with libraries of thousands of tracks, there's no reliable way to know the **vibe** of a track before playing it. Genre tags are broad, and manually tagging every track's intensity, mood, and sonic characteristics doesn't scale.

This project builds an automated classification system that:

1. **Learns the DJ's own classification code** (genre, style, intensity, sound elements, descriptors) from the ~2,100 tracks already hand-labeled, and **predicts it for the rest of the library** (and for every new track)
2. **Recommends similar music** — given a track, surface others with comparable feel based on learned audio features
3. **Delivers the predictions** — first as a report file (song → predicted code), later written into a separate metadata field of each track so it is visible in DJ software (Traktor, Rekordbox)

### Target Users

- **Primary:** Personal use (library at `D:\Le musiqe\Tracks`, ~4,270 audio files / ~3,500 unique tracks, scaling to 10,000+)
- **Future:** Potentially public-facing for other DJs

### Success Criteria

- Predicted labels **agree with the manual labels** on held-out tracks (measured per label, see §11)
- Tracks grouped in the same cluster should **sound like they belong together** when listened to back-to-back
- Tags should be **useful for live set preparation** — filtering by "progressive + fly + intensity 3 + congas" gives a coherent set
- The system supports **iterative experimentation** — easy to try approaches and compare them with the same scores
- **Final goal:** separate each track into stems, cluster each stem, and determine **with evidence which stem (drums, bass, other, vocals) carries the most value** for each part of the classification — then weight the final model accordingly

---

## 2. The Existing Label System (Ground Truth)

The DJ already classifies tracks with a personal code stored in the **Genre metadata field**. This is the ground truth the system learns from, and it is also the output format the system should reproduce.

### 2.1 Full format (≈2,140 tracks)

```
 2 2 3 1"#006000 groovy cong hype
 │ │ │ └───┬───┘ └──────┬───────┘
 │ │ │     │            └─ descriptors (free words, multi-tag)
 │ │ │     └─ 9-position sound-element code
 │ │ └─ intensity (1–4)
 │ └─ style
 └─ genre
```

| Position | Field | Values |
|----------|-------|--------|
| 1 | **Genre** | 1 = deep · 2 = progressive · 3 = techno · 4 = trance |
| 2 | **Style** | 1 = deep · 2 = fly/melodic · 3 = groovy · 4 = hype |
| 3 | **Intensity** | 1–4 (ordinal) |
| 4 | **Sound-element code** | 9 characters, one per element (below) |
| 5+ | **Descriptors** | free words: groovy, fly, hipnotic, cong, hype, chill, acid, dark, loaded, bigdrop, leger, ctd, strytllng, ... (glossary in §2.2) |

**Sound-element code** — position *n* describes element *n*:

- `0` → element absent
- digit *n* → element present
- the character typed with **Shift + n** (e.g. `!` `"` `#` `$` `%` `&` ...) → element present and **more intense**

| Pos | Element | Pos | Element | Pos | Element |
|-----|---------|-----|---------|-----|---------|
| 1 | Bass | 4 | Low pads / constant low bass | 7 | Tech-house hi-hats |
| 2 | Pads (long notes) | 5 | Vocals | 8 | Minimal sounds |
| 3 | Latin percussion / congas (non-square rhythm) | 6 | Acid sounds | 9 | Intense techno |

Examples: `1"#006000` = bass, pads (intense), congas (intense), acid. `120406000` = bass, pads, low pads, acid.

**Parsing rule:** decode **by position, not by character** — `0` = absent, the matching digit = present, any other non-digit symbol = intense. This makes the parser independent of keyboard layout (e.g. Shift+9 may appear as `(`). Entries whose digit doesn't match its position are flagged as malformed.

### 2.2 Descriptor glossary

Descriptors fall into four groups. The group matters for modeling: **structural** descriptors describe *how the track evolves* (drops, breaks), so a single whole-track average can't capture them — they need the per-window embeddings (§8).

| Group | Descriptor | Meaning |
|-------|-----------|---------|
| **Structure** | `ctd` | "Chill till drop" — energy stays low until the drop (most tracks have two drops, the second raising the energy; in these the energy stays low until the drop) |
| | `hungup` | A long stretch without kick before the drop |
| | `bigdrop` | The drop carries a lot of energy (`flydrop`: variant) |
| **Character / mood** | `strytllng` | "Storytelling" — a present, emotional melody that evolves along the track |
| | `ooc` | "Out of context" — weird, unusual track |
| | `groovy`, `fly`, `hipnotic`, `hype`, `chill`, `dark`, `loaded`, `spaced`, `melanc` | Mood / feel words (overlap with style values) |
| **Sound** | `strkick` | "Strong kick" |
| | `komanperc` | Percussion made of short, stab-like grooves |
| | `cong` / `conga` | Latin percussion / congas (same as element 3) |
| | `psh` | White noise sustained for long continuous periods (not percussive) |
| | `acid`, `arpgroove`, `clap`, `speech` | Specific sounds |
| | `melodic9` | Melodic techno |
| **Artist style** | `dgwd` | "Dig Weed" (John Digweed) style — also the DW folders/categories |
| | `leger` | An artist's style: percussive/conga-driven melody with a lot of energy |
| | `orion` | Alex Orion style: usually fly, groovy, low energy |
| | `arias` | Ezequiel Arias style |
| | `aliaga` | Ivan Aliaga style |
| | `zooz`… | Other rare artist-style words — confirmed during the label review |

Normalization: lowercase; split compounds (`fly-conga-leger` → `fly`, `conga`, `leger`); merge variants (`cong` = `conga`, `hypnotic` = `hipnotic`). Artist-style descriptors are also natural **similarity queries** ("more tracks that sound like `orion`").

### 2.3 Other formats found

| Format | Example | Count | Handling |
|--------|---------|-------|----------|
| Three numbers only | `2 2 3` | ~360 | Genre, style, intensity — no element code |
| Short format | `4 3 hype cong leger` | ~60 | Old set-building group + intensity + descriptors → **intensity and descriptors used**; group ignored |
| Plain text genre | `Progressive House`, `Electro` | ~440 | Written by stores/other software — **not** ground truth |
| Empty | — | ~1,245 | Unlabeled (includes all 310 WAV files) |

**Set category / folders — not predicted.** The short format's first number and the `classified…` folders are the DJ's set-building groups (DWStart, Deep, Organic, Simon, spaceCong, Zooz, Galvan, SpaceMotion, DW, Indi). The DJ decided they **don't need to be predicted**. Folders whose meaning isn't clear (`000Deep`, `06Minimal`, `haus`, `Remolinos`, `Best2024`, `LemAdDy`, `classifiedNew`) are **excluded from training** and kept as an **unseen test set**: once the models work, their predictions on these folders are checked by the DJ (§11, Phase 2).

### 2.4 Label statistics (scan of 2026-09-24, full-format tracks)

| Label | Distribution | Learnability |
|-------|-------------|--------------|
| Genre | deep 42 · **progressive 1,805** · techno 272 · trance 24 | Very imbalanced — deep/trance need more examples |
| Style | deep 188 · **fly/melodic 1,419** · groovy 471 · hype 65 | OK; hype is small |
| Intensity | 1: 101 · 2: 740 · 3: 983 · 4: 319 | Good |
| Bass | absent 114 · present 1,215 · intense 814 | Good |
| Pads | absent 141 · present 769 · intense 1,233 | Good |
| Latin perc / congas | absent 684 · present 558 · intense 901 | Good |
| Low pads | absent 1,891 · present 179 · intense 73 | Learnable (present vs. absent) |
| Vocals | absent 1,622 · present 305 · intense 216 | Good |
| Acid | absent 652 · present 879 · intense 612 | Good |
| Tech-house hi-hats | 34 positive | **Too few** — collect more before training |
| Minimal sounds | 17 positive | **Too few** |
| Intense techno | 93 positive | Borderline |
| Descriptors | 252 distinct words; ~15 with ≥ 30 uses | Train only frequent ones after normalization (§2.2) |

### 2.5 Data-quality issues to handle

- **Duplicates:** ~508 file names appear more than once (~1,240 files) — the same track copied into several folders (`Years/...` and `classified/...`). Tracks must be **deduplicated by audio content**, and duplicates must never be split between training and test data (otherwise scores are inflated).
- **Mixed formats:** some entries are irregular (`2  3  103006000  2`, `2 3 120006000 4`). The parser reports them for manual fixing instead of guessing.
- **Formats:** MP3 (3,903), WAV (310), AIFF (32), FLAC (16). WAV files carry no tags here.
- **Store-written genres** ("Electro", "House") are ignored as labels.

---

## 3. Output: Predicted Tags

### Phase 1 — report file (no file modification)

The first deliverable is a **report**, written by the CLI:

- `predictions.csv` — one row per track: path, artist, title, predicted code, per-field confidence, manual code (if any), agreement flag
- `predictions.md` — human-readable version grouped by genre/style, highlighting low-confidence and disagreeing tracks

Predictions use **the same code format** as the manual labels (e.g. `2 2 3 1"#006000 groovy cong`), so they can be read at a glance.

### Phase 2 — separate metadata field (opt-in)

Once predictions are trusted, the predicted code is written to a **separate field** — never to `Genre`:

- A visible field (`Grouping` or `Comment` — confirmed by testing what Traktor/Rekordbox display), prefixed to mark it as predicted, e.g. `AI: 2 2 3 1"#006000 groovy`
- A machine-readable copy in custom `TXXX` (ID3) / Vorbis comment fields (`MC_CODE`, `MC_CONFIDENCE`, `MC_VERSION`)

Rules: **the Genre field is never modified**; original tags are backed up; dry-run first. Promoting a reviewed prediction into Genre is a manual, per-track decision.

---

## 4. MVP: Experimentation Platform

The MVP is a **workbench** for testing classification approaches, not a polished product.

### MVP Scope

- **CLI** to run each pipeline stage (ingest, embed, train, cluster, separate, report)
- Web interface with a **track table** (artist, track, BPM, key, manual code, predicted code, confidence, cluster)
- **Filters** by any label field (genre, style, intensity, elements, descriptors)
- **Audio snippet playback** for quick validation
- **Run comparison** — scores of different pipelines side by side
- **Cluster visualization** (2D scatter plots)
- **Review queue** — least-confident / disagreeing predictions first; corrections become new labels
- **Stem value report** — which stem contributes most to each label

### What's NOT in the MVP

- Public access / authentication
- Cloud storage or streaming integration
- Mobile support
- Automated reprocessing on library changes

---

## 5. Recommended Tech Stack

Python-native for the experimentation phase. Full details in [architecture/tech-stack.md](architecture/tech-stack.md).

- **Project:** Python 3.12, managed with **uv**; stages run from a **Typer CLI**
- **Inference:** ONNX Runtime + DirectML for embedding models; PyTorch (via `audio-separator`) for source separation
- **Embeddings:** **Discogs-EffNet** (primary) and **MAEST** (comparison) — music-specific models trained on the Discogs style taxonomy, published as ONNX by the Essentia/MTG team
- **Classification:** scikit-learn (logistic regression / small MLP probes, HDBSCAN, metrics) + umap-learn
- **UI:** Streamlit + Plotly (reads results, plays audio, review queue)
- **Storage:** SQLite (tracks, labels, runs, metrics) + NumPy/HDF5 files (embeddings) + FLAC stems

### Production Phase (Future): FastAPI + React

FastAPI backend, React + TanStack Table frontend, PostgreSQL + pgvector. The ML pipeline stays the same — only the interface layer changes.

---

## 6. Technical Architecture

### Hardware Constraints

- **GPU:** AMD Radeon RX 7600 (8GB VRAM, RDNA3 Navi 33, **gfx1102**)
- **ROCm:** As of ROCm 7.2 the RX 7600 is **not** on AMD's official support list for Windows or Linux (supported RDNA3 parts are gfx1100/gfx1101: RX 7700–7900). Re-check each ROCm release; if gfx1102 is added, PyTorch-on-ROCm becomes an option for source separation.
- **Primary GPU path:** ONNX Runtime with DirectML — works on any DirectX 12 GPU.
  - Microsoft has moved DirectML to sustained engineering; new investment goes to **Windows ML** (ONNX Runtime with vendor execution providers). DirectML is fine for this project, but all inference goes through one `create_session()` wrapper so the provider can be swapped later.
- The embedding models (EffNet, MAEST) are small enough that **CPU inference is viable**. The GPU matters most for **source separation**.

### Platform Notes (Windows 11)

- **Essentia has no Windows wheels** (Linux/macOS only). The `essentia` package is not a runtime dependency:
  - Essentia's models run from their **published `.onnx` files** with ONNX Runtime.
  - The mel-spectrogram input is re-implemented in NumPy/librosa and validated once against real Essentia output inside **WSL2** (one-off parity test, EPIC-3).
- **BPM and key** are **read from existing tags** (~80% of tracks already have them, from Rekordbox/Traktor analysis). Computing them is only a fallback.
- **Runtime data lives outside OneDrive.** The repo is in a OneDrive folder; SQLite, embeddings and stems go to a local directory (e.g. `D:\MusicClassData\`) set by environment variable. OneDrive sync locks and can corrupt open database files.
- **The audio library is read-only** for every stage except the (opt-in) tag writer.

### Architecture Overview

A **staged pipeline**; each stage is a CLI command, independently runnable and cached.

```
┌─────────────────────────────────────────────────────────────────┐
│  STAGE 1: INGEST & LABELS                                       │
│  • Scan D:\Le musiqe\Tracks; dedupe by audio-content hash       │
│  • Read tags: artist, title, BPM, key, Genre                    │
│  • Parse the label code → genre/style/intensity/elements/       │
│    descriptors; unclear folders held out as unseen test set     │
│  • Label report (distributions, malformed entries, duplicates)  │
│  • Decode + resample once to 16 kHz mono (cached)               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│  STAGE 2: EMBEDDINGS — FULL MIX (ONNX + DirectML / CPU)         │
│  • Discogs-EffNet (1280-d) per ~2 s patch → per-window matrix   │
│  • Structure-aware pooling (skip DJ intro/outro) → track vector │
│  • Discogs-400 style activations kept as extra features         │
│  • MAEST as comparison model                                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│  STAGE 3: CLASSIFICATION (supervised) + EXPLORATION (clusters)  │
│  • One model per label field, trained on the manual code        │
│  • Grouped cross-validation → per-label scores                  │
│  • UMAP + HDBSCAN for discovery, mislabel detection, viz        │
│  • Report file: song → predicted code + confidence              │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│  STAGE 4: SOURCE SEPARATION (final-goal phase)                  │
│  • audio-separator: Demucs htdemucs_ft (4 stems) — primary      │
│  • BS/Mel-RoFormer as quality comparison                        │
│  • PyTorch + DirectML (experimental) with CPU fallback          │
│  • Stems stored as mono FLAC                                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│  STAGE 5: PER-STEM EMBEDDINGS, CLUSTERING & STEM VALUE          │
│  • Same embedding model on drums / bass / other / vocals        │
│  • Per-stem UMAP + HDBSCAN → sonic sub-groups                   │
│  • Stem value analysis per label field (§10)                    │
│  • Output: stem weight vector per label → final model           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│  STAGE 6: EXPORT, SIMILARITY & VISUALIZATION                    │
│  • Report file (CSV + MD); later opt-in separate tag field      │
│  • Similarity search (NumPy cosine; sqlite-vec/FAISS at scale)  │
│  • Plotly scatter plots, stem value charts                      │
└─────────────────────────────────────────────────────────────────┘
```

The **full-mix pipeline is completed and scored before source separation is added**. That gives the stem analysis a baseline: a stem is only "valuable" if it adds information the full mix doesn't already carry.

### Track Identity

Tracks are identified by a **hash of the decoded audio samples** (not the file path, not the raw file bytes):

- Path hashes break when files are moved or renamed, and would count the ~700 duplicate copies as different tracks.
- Raw-file hashes change whenever tags are written.
- An audio-content hash survives both. All paths of a track are stored in a separate `track_files` table.
- Future improvement: acoustic fingerprint (Chromaprint) to also catch the same track in different encodings/bitrates.

### Caching Strategy

| Stage | Cache Format | Rerun Cost (~3,500 unique tracks) |
|-------|-------------|------------|
| Ingest (tags, labels) | SQLite rows | Minutes |
| Decoded 16 kHz audio | `.npy` / FLAC per track | One-off decode |
| Full-mix embeddings | `.npy` per track (windows) + pooled matrix | To benchmark; expected tens of minutes |
| Source separation | Mono FLAC stems on disk | To benchmark; budget 20–90 s/track |
| Per-stem embeddings | Same as full-mix | ~4× full-mix cost |
| Classification / clustering | SQLite (results, params, metrics) | Seconds |

New tracks go through only the expensive stages they're missing; classification reruns on the full dataset.

---

## 7. Source Separation: What's Possible Today

No open-source model separates synth sub-categories (pads, stabs, leads, acid). Tools output 4–6 stems, with synths grouped in "other".

| Tool / Model | Stems | Runtime | Notes |
|--------------|-------|---------|-------|
| **Demucs v4 htdemucs_ft** | 4: vocals, drums, bass, other | PyTorch | Reliable 4-stem baseline in one pass; bag of 4 models → slower |
| **Demucs htdemucs_6s** | 6: + guitar, piano | PyTorch | Piano/guitar rarely useful in this music |
| **BS-RoFormer / Mel-RoFormer (UVR)** | Mostly 2 (vocals/instrumental); multi-stem variants exist | PyTorch | Best vocal quality (SDR > 12.9 dB); can be chained (vocals first, then Demucs on the instrumental) |
| **MDX-Net (UVR)** | 2 | **ONNX** | Only natively-ONNX UVR family; 2-stem only |

**Correction from Revision 1:** RoFormer models are **PyTorch** checkpoints in `audio-separator`/UVR, not ONNX.

**Approach:** `audio-separator` with the `[dml]` extra (`use_directml`, experimental, community-supported); CPU fallback. Benchmark on 20 tracks (EPIC-4) before running the full library.

```python
from audio_separator.separator import Separator

separator = Separator(output_dir=stems_dir, output_format="FLAC", use_directml=True)
separator.load_model(model_filename="htdemucs_ft.yaml")
stem_paths = separator.separate("track.mp3")  # vocals, drums, bass, other
```

**Memory:** segment-based processing to fit 8 GB VRAM.
**Storage:** 4 mono stems × ~7 min × 3,500 tracks ≈ 250 GB as FLAC (≈ 500 GB as WAV). Disk space was declared unconstrained, but FLAC halves it at no cost.

### Element → stem mapping (what the stems should reveal)

The DJ's element code maps naturally onto stems — this is what makes the stem analysis testable:

| Element (code position) | Expected stem |
|-------------------------|---------------|
| 1 Bass, 4 Low pads / constant bass | **bass** |
| 3 Latin percussion / congas, 7 Tech-house hi-hats | **drums** |
| 2 Pads, 6 Acid, 8 Minimal sounds | **other** (acid may bleed into bass) |
| 5 Vocals | **vocals** |
| 9 Intense techno | drums + other |

---

## 8. Embedding Models

### Primary: Discogs-EffNet (Essentia / MTG)

- Trained on **Discogs' 400-style taxonomy**, which includes this library's styles (Progressive House, Deep House, Melodic House & Techno, Trance, Techno ...) — far closer to the task than general-purpose audio models
- **1280-dimensional** embeddings; input is 16 kHz audio → 96-band mel patches (~2 s each)
- **Published as ONNX** (`discogs-effnet-bsdynamic-1.onnx`, dynamic batch) — no conversion needed
- The companion **genre_discogs400** head gives 400 style probabilities — extra interpretable features
- Small and fast: viable on CPU; DirectML makes it faster

### Comparison: MAEST

- Transformer trained on the same Discogs taxonomy; 5/10/20/30 s context variants
- ONNX versions with dynamic batch are published
- Heavier; often stronger on style classification — A/B tested on the manual labels

### Optional baseline: PANNs CNN14

- The Revision 1 choice. Trained on AudioSet (general sounds). Kept only as a lower baseline; needs own PyTorch → ONNX conversion.

### Pretrained heads (extra features)

Essentia publishes small heads on top of these embeddings: **arousal/valence**, **mood** (aggressive, happy, party, relaxed, sad), **danceability**, **approachability/engagement**. Their outputs are cheap extra features, especially for intensity and descriptors.

### Future: text-audio models

- **CLAP (music variant)**, **MuQ-MuLan** — natural-language search and zero-shot tagging ("rolling acid bassline", "latin congas"). Post-MVP; useful comparison for the element code.
- **MERT / MuQ** — stronger general music representations; revisit if EffNet/MAEST plateau.

### Windowing and pooling (important for this genre)

Progressive house tracks are long (6–9 min) with 1–2 minute DJ intros/outros that are mostly drums. A whole-track average dilutes the character.

- Embed every ~2 s patch and **store the per-window matrix**
- Track vector via **structure-aware pooling**: drop first/last ~10% (configurable); compare mean vs. energy-weighted vs. mean+std
- Per-window data also allows element **presence over time** and Energy Curve later

### Low-level features (hybrid vector)

Cheap, interpretable features appended to embeddings, mostly for **intensity**: RMS loudness, onset density, spectral flux, spectral centroid, low-band energy ratio, BPM.

### Model Comparison

| Model | Dim | Sample Rate | ONNX | Trained On | Role |
|-------|-----|-------------|------|-----------|------|
| **Discogs-EffNet** | 1280 | 16 kHz | Published | Discogs styles | **Primary** |
| **MAEST** | 768 | 16 kHz | Published | Discogs styles | Comparison |
| **PANNs CNN14** | 2048 | 32 kHz | Convert yourself | AudioSet | Baseline |
| **CLAP (music)** | 512 | 48 kHz | Export | Music + text | Post-MVP |

---

## 9. Classification Approach

### 9.1 One model per label field — supervised first

With ~2,100 fully coded tracks, each part of the code gets its own small model on top of the embeddings:

| Label field | Task | Model | Metric |
|-------------|------|-------|--------|
| Genre (4) | Multi-class, imbalanced | Logistic regression, `class_weight="balanced"` | Macro-F1, per-class recall |
| Style (4) | Multi-class | Logistic regression | Macro-F1 |
| Intensity (1–4) | Ordinal | Ridge / ordinal regression (+ low-level features) | MAE, Spearman, exact/±1 accuracy |
| Elements 1–6 (+9) | 9 × {absent, present, intense} | One ordinal model (or two binary: present?, intense?) per element | Macro-F1 per element; AUC for present |
| Elements 7, 8 | Too few examples | Not trained yet — flagged for labeling | — |
| Descriptors | Multi-label | One binary classifier per word with ≥ 30 examples. Structural ones (`ctd`, `hungup`, `bigdrop`) use per-window features (e.g. energy/kick-presence curve statistics) | Per-tag F1 / AP |

```python
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import StratifiedGroupKFold, cross_val_predict
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

model = make_pipeline(StandardScaler(), LogisticRegression(max_iter=2000, class_weight="balanced"))
cv = StratifiedGroupKFold(n_splits=5, shuffle=True, random_state=0)
pred = cross_val_predict(model, X, y_style, groups=track_ids, cv=cv)
```

- **Grouped CV**: duplicates share a group so the same audio is never in both train and test
- Trains in seconds → every embedding/pooling/stem choice gets a score for every label
- Predicted probabilities give **confidence** → review queue shows least-confident and disagreeing tracks first (active learning)
- **Predictions for the ~1,400 unlabeled/partially-labeled tracks** are assembled back into the DJ's code format for the report
- Partially labeled tracks (three numbers only, short format) contribute to the fields they have

### 9.2 Clustering — discovery, not labeling

UMAP + HDBSCAN is still valuable to:

- **Discover sub-styles** not yet in the code
- **Find likely mislabels** (a track that sits among tracks with a different code)
- **Visualize** the library in 2D, colored by any label field
- **Suggest new descriptors** (listen to a cluster, name it)

HDBSCAN is part of scikit-learn (`sklearn.cluster.HDBSCAN`, ≥ 1.3).

```python
import umap
from sklearn.cluster import HDBSCAN

reduced = umap.UMAP(n_neighbors=30, min_dist=0.0, n_components=15, metric="cosine").fit_transform(X)
labels = HDBSCAN(min_cluster_size=30, min_samples=10, cluster_selection_method="eom").fit_predict(reduced)
```

Clusters are scored **against the manual labels** (AMI/ARI per field) and with internal metrics (silhouette, DBCV).

### 9.3 Intensity refinement (optional)

The manual intensity is a 1–4 scale. If finer granularity is wanted later (e.g. ordering tracks within intensity 3), **pairwise comparisons** ("which of these two is more intense?") in the review UI + a Bradley-Terry model give a continuous score that the regressor can learn.

---

## 10. Stem Analysis & Stem Value (Final Goal)

**Goal:** separate each track into stems, cluster each stem, and determine **which stem carries the most value for the overall classification** — then weight the final model with it.

### 10.1 Per-stem clustering

For each stem (drums, bass, other, vocals):

- Extract embeddings with the same model and pooling as the full mix
- UMAP + HDBSCAN **across the whole library** per stem (per-genre subsets are too small for stable density clustering, given 84% of tracks are progressive). Cross-tabulate stem clusters with the label fields afterwards.
- Listen to cluster centers → name sonic sub-groups (e.g. drums: straight / latin-conga / broken; bass: rolling / sub / acid; other: lush pads / acid lines / dark minimal)

### 10.2 Measuring stem value

Stem value is measured **per label field** — each stem may matter for a different part of the code (e.g. drums for intensity and congas, "other" for style and pads). Five complementary measurements, all on the same grouped CV folds, compared against the **full-mix baseline**:

| # | Method | Question it answers |
|---|--------|---------------------|
| 1 | **Single-stem probe** — train each label model on one stem's embedding alone | How much information about this label does each stem carry on its own? |
| 2 | **Leave-one-stem-out ablation** — model on [mix + all stems], drop one stem at a time | What unique information does each stem add beyond the others and the mix? |
| 3 | **Learned fusion weights** — stacking: per-stem model probabilities combined by a logistic regression; grouped permutation importance | How much weight does an optimal model give each stem? |
| 4 | **Cluster agreement** — AMI/ARI between each stem's clusters and each label field | Do the unsupervised stem clusters align with the DJ's categories? |
| 5 | **Element → stem check** — is each element best predicted from its expected stem (§7 table)? | Does separation isolate what the DJ hears? (A sanity check on separation quality) |

**Output:** a **stem weight vector per label field**, e.g.

| Label | Mix | Drums | Bass | Other | Vocals |
|-------|-----|-------|------|-------|--------|
| Style | 0.40 | 0.15 | 0.10 | 0.30 | 0.05 |
| Intensity | 0.30 | 0.35 | 0.20 | 0.15 | 0.00 |
| Congas (el. 3) | 0.20 | 0.70 | 0.00 | 0.10 | 0.00 |

*(illustrative values only)*

The final model uses these weights (weighted concatenation of L2-normalized embeddings, or the stacking model directly), and the stem value report is shown in the UI.

---

## 11. Experimentation Plan

Start simple; add complexity only when it measurably helps. Every phase is scored per label field on grouped 5-fold CV, plus listening.

### Phase 0: Ingest & Labels

- Scan, dedupe, read tags, parse the code; mark tracks in unclear folders as the held-out test set
- **Label report**: distributions, malformed entries to fix by hand, duplicates
- Fix the malformed entries worth fixing; decide minimum class sizes

### Phase 1: Full-Mix Baseline + First Report

- Discogs-EffNet embeddings (windowed); compare pooling strategies
- One model per label field → **baseline scores**
- **First `predictions.csv` / `predictions.md`** for the unlabeled tracks
- UMAP + HDBSCAN exploration; AMI vs. labels; mislabel candidates
- Validation: CV scores + listening to low-confidence predictions

### Phase 2: Model Comparison & Review Loop

- MAEST vs. EffNet on the same folds; optional PANNs baseline
- Review queue in the UI: corrections become labels → retrain
- Grow rare classes (deep, trance, hype, tech-house hi-hats, minimal) through targeted review
- **Unseen-folder test:** predict the held-out unclear folders (`000Deep`, `06Minimal`, `haus`, `Remolinos`, `Best2024`, `LemAdDy`, `classifiedNew`) and have the DJ judge the predictions

### Phase 3: Source Separation & Stem Value (final goal)

- Benchmark separation models on 20 tracks; separate the library in resumable batches
- Per-stem embeddings; per-stem clustering
- Stem value analysis (§10.2) → stem weight vectors
- Validation: does the stem-weighted model beat the full-mix model per label? Do stem clusters sound coherent?

### Phase 4: Refinement & Tag Export

- Final model = mix + weighted stems (where they help)
- Opt-in writing of the predicted code into a separate field (Genre untouched)
- Test sorting/filtering in DJ software
- Validation: build a coherent 1-hour set using only the filters

### Validation Methods

| Method | What It Measures | When |
|--------|-----------------|------|
| **Grouped CV vs. manual labels** | Macro-F1 / MAE per label field | Every configuration |
| **Low-confidence & disagreement review** | Ambiguous vs. mislabeled vs. model error | After each model update |
| **Cluster listening test** | Do grouped tracks sound similar? | After clustering runs |
| **Cluster–label agreement** | AMI / ARI per label field | Comparing clustering setups |
| **Internal metrics** | Silhouette, DBCV | Tuning UMAP/HDBSCAN |
| **Stem ablation & element→stem check** | Unique contribution of each stem | Phase 3 |
| **Set preparation test** | Coherent 1-hour set from filters | Phase 4 |

---

## 12. Processing Time Estimates

Planning ranges — each replaced by a real benchmark in its epic. For ~3,500 unique tracks:

| Stage | GPU (RX 7600, DirectML) | CPU Only |
|-------|---------------|----------|
| Ingest (read tags, hash, decode + resample) | ~1 hour (CPU-bound) | same |
| Full-mix embeddings (EffNet) | ~20–45 min | ~1.5–3 hours |
| Source separation (htdemucs_ft) | ~20–90 hours (benchmark!) | ~70–140 hours |
| Per-stem embeddings (4 stems) | ~1.5–3 hours | ~6–12 hours |
| Classification / clustering | seconds | seconds |

Phases 0–2 need no source separation: the first full loop (ingest → embeddings → scores → report) is a couple of hours of processing. Separation runs unattended overnight in resumable batches.

---

## 13. Tools and Models Worth Watching

- **Windows ML** — Microsoft's successor path to DirectML; vendor EPs for AMD
- **ROCm gfx1102 support** — would make PyTorch on the RX 7600 a first-class option for separation
- **MuQ / MuQ-MuLan, MERT** — stronger music embeddings and music-text models
- **CLAP (music)** — natural-language search, zero-shot element tagging
- **Existing products to benchmark against:** Mixed In Key (energy level), Lexicon DJ (library management across DJ apps), Cyanite (commercial auto-tagging)

---

## 14. Revision History

### Revision 2 — 2026-09-24

| Change | Reason |
|--------|--------|
| Ground truth = the DJ's **personal code in the Genre field** (genre, style, intensity, 9 sound elements, descriptors). Documented, with descriptor glossary. Set category is **not** predicted; unclear folders are an unseen test set. | Library scan: ~2,140 fully coded tracks, ~420 partially coded; DJ's answers |
| Supervised models per label field become the primary method; clustering used for discovery | Labels give objective scores and a direct way to predict the code |
| First output = **report file** (CSV + MD); tag writing later, to a separate field; **Genre never modified** | DJ's request; Genre holds ground truth |
| Deduplication by audio content + **grouped CV** | ~1,240 files are duplicate copies across folders; would inflate scores |
| Primary embedding model: PANNs → **Discogs-EffNet** (MAEST as comparison) | Music-specific, trained on Discogs styles; ONNX published |
| Essentia via its ONNX models, not the Python package | Essentia has no Windows wheels |
| BPM/key read from existing tags | ~80% already present |
| Loudness normalization to -14 LUFS removed | Not needed for embeddings |
| Windowed embeddings + structure-aware pooling | Long DJ intros/outros dilute averages |
| Source separation after the full-mix baseline; **stem value analysis** (5 methods, incl. element→stem check) as final goal | A stem is valuable only if it beats the baseline; value is measured per label |
| Separation: htdemucs_ft primary, RoFormer comparison; PyTorch allowed for separation | RoFormer is PyTorch in audio-separator; Demucs gives 4 stems in one pass |
| Stems as mono FLAC | Half of WAV size, lossless |
| Track ID = hash of decoded audio | Path hashes break on move and count duplicates twice |
| HDBSCAN from scikit-learn; FAISS only at scale | Fewer dependencies |
| Pipeline runs from CLI; Streamlit reads results | Long jobs inside Streamlit are fragile |
| Runtime data outside OneDrive; library treated as read-only | Sync corruption; safety |
| AIFF added to supported formats | 32 AIFF files in the library |
| GPU id corrected to gfx1102; ROCm re-verified (7.2: still unsupported) | Revision 1 listed gfx1032 (RX 6600) |

### Revision 1 — 2026-02-11

Initial analysis.
