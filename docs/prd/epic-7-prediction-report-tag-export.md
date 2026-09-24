# EPIC-7: Prediction Report & Tag Export

> **Priority:** P0 (7a report) · P2 (7b tag writing)
> **Estimated Effort:** Small (7a) · Medium (7b)
> **Phase:** Phase 1 (7a) · Phase 4 (7b)
> **Dependencies:** EPIC-5
> **Can Start After:** EPIC-5
> **Sprint:** 2 (7a) · 5 (7b)
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §3 (Rev. 2)
> **Replaces:** Revision 1 "Tag System & Metadata Export"

---

## Goal

Deliver predictions to the DJ — first as files, later (opt-in) inside each track's metadata in a **separate** field. The `Genre` field is never modified.

---

## Scope

### 7a — Report (first deliverable)
- `reports/predictions.csv`: path, artist, title, predicted code, per-field confidence, manual code, agreement flag
- `reports/predictions.md`: grouped by genre/style; uncertain and disagreeing tracks highlighted
- `labels/formatter.py` reproduces the DJ's format exactly, e.g. `2 2 3 1"#006000 groovy cong`
- Regenerated after every training run

### 7b — Tag writing (opt-in, later)
- Visible field: `Grouping` or `Comment`, prefixed `AI: ` (e.g. `AI: 2 2 3 1"#006000 groovy`)
- Machine-readable: `TXXX:MC_CODE`, `MC_CONFIDENCE`, `MC_VERSION` (ID3) / Vorbis equivalents (FLAC)
- Formats: MP3, AIFF (ID3), FLAC (Vorbis), WAV (ID3 chunk — verify DJ software reads it)
- Dry-run by default; `--write` required; original values backed up in `backups/`; `rollback` command
- Verify display/sort in Traktor and Rekordbox

---

## Technical Context

```python
from mutagen.id3 import ID3, TXXX, TIT1

tags = ID3(path)
tags.add(TXXX(encoding=3, desc="MC_CODE", text=['2 2 3 1"#006000 groovy cong']))
tags.add(TXXX(encoding=3, desc="MC_CONFIDENCE", text=["0.82"]))
tags.add(TIT1(encoding=3, text=['AI: 2 2 3 1"#006000 groovy cong']))  # Grouping
tags.save()   # TCON (Genre) untouched
```

Neither Traktor nor Rekordbox displays custom `TXXX` frames — hence the visible Grouping/Comment copy. Using the DJ's own code format keeps predictions readable at a glance.

---

## Acceptance Criteria

- [ ] (7a) Report files generated after every training run
- [ ] (7a) Formatter round-trips: `format(parse(s)) == s` for every well-formed manual code
- [ ] (7b) Only target fields change; Genre and all other tags byte-identical; rollback restores originals
- [ ] (7b) Predicted field visible and sortable in the DJ software

---

## Key Technical Decisions

- Grouping vs. Comment (test in both DJ apps; Comment is often already used — 2,600 files have one)
- Promotion of reviewed predictions into Genre: manual, per track, never automatic

---

## Downstream Epics

- None (end of the delivery chain)
