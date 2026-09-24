# EPIC-4: Source Separation Pipeline (Stage 4)

> **Priority:** P1 — needed for the final goal
> **Estimated Effort:** Large
> **Phase:** Phase 3 (benchmark can start in Sprint 3)
> **Dependencies:** EPIC-1, EPIC-2
> **Can Start After:** EPIC-2
> **Sprint:** 3 (benchmark) · 4 (full library)
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §7 (Rev. 2)

---

## Goal

Split every track into drums, bass, other and vocals stems, reliably and resumably, within 8 GB VRAM.

---

## Scope

- `audio-separator[dml]` as optional dependency group `separation`
- **Benchmark on 20 tracks:** `htdemucs_ft` vs. RoFormer chain (vocals RoFormer → Demucs on instrumental); DirectML vs. CPU; s/track, VRAM, listening check
- Segment-based processing for 8 GB VRAM
- Mono FLAC output: `stems/<track_id>/{drums,bass,other,vocals}.flac`
- Residual check: sum of stems ≈ original mix
- Resumable overnight batches; per-track status; failures logged and retried
- Option: separate only a representative section if full tracks are too slow (decided by benchmark)

---

## Technical Context

| Model | Stems | Runtime | Notes |
|-------|-------|---------|-------|
| Demucs `htdemucs_ft` | 4 | PyTorch | Primary candidate; one pass |
| Demucs `htdemucs_6s` | 6 | PyTorch | Guitar/piano rarely useful here |
| BS/Mel-RoFormer | 2 (multi-stem variants exist) | PyTorch | Best vocals; chain with Demucs |
| MDX-Net | 2 | ONNX | Only native-ONNX UVR family |

**Correction from Revision 1:** RoFormer is PyTorch (not ONNX) in audio-separator. PyTorch is therefore allowed for this epic.

```python
from audio_separator.separator import Separator

separator = Separator(output_dir=stems_dir, output_format="FLAC", use_directml=True)
separator.load_model(model_filename="htdemucs_ft.yaml")
separator.separate(str(track_path))
```

DirectML support in audio-separator is **experimental / community-supported** — the benchmark must confirm stability; CPU is the fallback.

**Why stems matter here:** the DJ's element code maps onto stems (bass/low pads → bass; congas/hi-hats → drums; pads/acid/minimal → other; vocals → vocals), which EPIC-10 uses to measure stem value.

**Estimates (to be replaced by benchmark):** 20–90 s/track on GPU → ~20–90 h for ~3,500 tracks. Storage ≈ 250 GB as mono FLAC.

---

## Acceptance Criteria

- [ ] Benchmark report with chosen model, s/track and VRAM
- [ ] Full library separated without VRAM crashes; interrupted runs resume
- [ ] Residual check passes for ≥ 98% of tracks; failures listed

---

## Key Technical Decisions

- Default model (by benchmark)
- Full track vs. representative section
- Keep vocals stem (yes by default: element 5 is vocals)

---

## Downstream Epics

- **EPIC-3:** per-stem embeddings
- **EPIC-10:** Stem Value Analysis
