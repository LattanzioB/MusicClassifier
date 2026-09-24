# EPIC-2: Library Ingestion & Label Parsing (Stage 1)

> **Priority:** P0
> **Estimated Effort:** Medium
> **Phase:** Phase 0
> **Dependencies:** EPIC-1
> **Can Start After:** EPIC-1
> **Sprint:** 1
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §2 (Rev. 2)
> **Replaces:** Revision 1 "Audio Preprocessing Pipeline" (BPM/key are now read from tags; loudness normalization dropped)

---

## Goal

Register every track in the library, deduplicate, read existing tags, and turn the DJ's label code into structured ground truth — producing a label report that drives all later decisions.

---

## Scope

- Scan `D:\Le musiqe\Tracks` for MP3 / WAV / AIFF / FLAC (≈4,270 files)
- `track_id` = hash of decoded audio; group duplicate files
- Read tags (mutagen): artist, title, BPM, key, Genre (raw), comment, grouping
- Label parser for all formats (below); malformed strings recorded with a reason, never guessed
- Held-out test set: tracks in unclear folders flagged and excluded from training/evaluation
- Descriptor normalization and grouping per the glossary (technical-analysis §2.2)
- Conflict detection: duplicate files with different codes
- Decode + resample to 16 kHz mono cache
- `label_report.md`
- Incremental rerun

---

## Technical Context

### The label code (full format)

```
2 2 3 1"#006000 groovy cong hype
│ │ │ └ 9-position element code   └ descriptors
│ │ └ intensity 1–4
│ └ style: 1 deep · 2 fly/melodic · 3 groovy · 4 hype
└ genre: 1 deep · 2 progressive · 3 techno · 4 trance
```

Element positions: 1 bass · 2 pads (long notes) · 3 latin perc/congas · 4 low pads/constant bass · 5 vocals · 6 acid · 7 tech-house hi-hats · 8 minimal · 9 intense techno.
Per position: `0` = absent, the digit = present, any other symbol (Shift+digit) = intense. **Decode by position**, not by symbol (keyboard-layout independent).

### Other formats

| Format | Example | Handling |
|--------|---------|----------|
| Three numbers | `2 2 3` | genre, style, intensity; elements unknown |
| Short format | `4 3 hype cong leger` | old set-building group (ignored — not predicted), intensity, descriptors |
| Plain text | `Progressive House` | store-written → not a label |
| Empty | — | unlabeled |
| Irregular | `2  3  103006000  2` | malformed → report |

### Scan results (2026-09-24)

- 4,266 audio files: MP3 3,903 · WAV 310 (no tags) · AIFF 32 · FLAC 16
- Full format ≈2,140 · three-numbers ≈360 · short ≈60 · plain text ≈440 · empty ≈1,245
- ~508 file names repeated across ~1,240 files (copies between `Years/…` and `classified…/…`)
- BPM present on ~3,370 files, key on ~3,480

### Descriptor glossary (summary — full table in technical-analysis §2.2)

- **Structure:** `ctd` chill till drop · `hungup` long stretch without kick before the drop · `bigdrop` high-energy drop
- **Character:** `strytllng` storytelling (emotional, evolving melody) · `ooc` out of context (weird track) · groovy, fly, hipnotic, hype, chill, dark…
- **Sound:** `strkick` strong kick · `komanperc` short stab-like percussion grooves · `cong` congas · `melodic9` melodic techno · acid
- **Artist style:** `dgwd` Digweed · `leger` percussive/conga melody with high energy · `orion` Alex Orion (fly, groovy, low energy)

### Label report contents

Counts per field/value · held-out folder tracks · format breakdown · malformed entries with paths · duplicate groups · conflicting codes within a duplicate group · tracks missing BPM/key · classes below the trainable minimum.

---

## Acceptance Criteria

- [ ] All files registered; duplicates collapse to unique `track_id`s
- [ ] Parser unit tests cover every format with real examples; ≥ 99% of full-format strings parse
- [ ] `label_report.md` generated and reviewed with the DJ; malformed entries fixed or accepted
- [ ] Rerun on an unchanged library changes nothing and is fast
- [ ] The library is not modified (integration test compares file hashes before/after)

---

## Key Technical Decisions

- Minimum examples per class (proposal: 20) and per descriptor (proposal: 30)
- Remaining unknown descriptors (`psh`, `arias`, `aliaga`…) confirmed in the report review
- Held-out folders (resolved): `000Deep`, `06Minimal`, `haus`, `Remolinos`, `Best2024`, `LemAdDy`, `classifiedNew` — excluded from training, used later as an unseen test

---

## Downstream Epics

- **EPIC-3:** Embedding Extraction (uses the decoded cache)
- **EPIC-5:** Classification (uses parsed labels)
- **EPIC-6:** UI (track browser can start from ingested data)
