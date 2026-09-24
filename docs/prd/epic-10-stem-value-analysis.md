# EPIC-10: Stem Value Analysis — FINAL GOAL

> **Priority:** P1 (the project's final goal)
> **Estimated Effort:** Large
> **Phase:** Phase 3
> **Dependencies:** EPIC-3 (per-stem embeddings), EPIC-4, EPIC-5, EPIC-9
> **Can Start After:** EPIC-4 + per-stem EPIC-3
> **Sprint:** 4
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §10 (Rev. 2)

---

## Goal

Cluster each separated stem and determine, **with evidence and per label field**, which stem (drums, bass, other, vocals) carries the most value for the classification — then weight the final model accordingly.

---

## Scope

### Per-stem clustering
- UMAP + HDBSCAN per stem across the whole library (per-genre subsets are too small: 84% progressive)
- Cross-tabulate stem clusters with every label field
- Listening sessions on cluster centers → name stem sub-groups (e.g. drums: straight / latin-conga / broken; bass: rolling / sub / acid; other: lush pads / acid lines / dark minimal)

### Stem value — five measurements
All on the fixed grouped folds (EPIC-9), compared against the **full-mix baseline** (EPIC-5):

| # | Method | Question |
|---|--------|----------|
| 1 | Single-stem probe per label field | How much does each stem know on its own? |
| 2 | Leave-one-stem-out ablation on [mix + all stems] | What unique information does each stem add? |
| 3 | Stacking fusion weights + grouped permutation importance | How much weight does an optimal model give each stem? |
| 4 | AMI/ARI of each stem's clusters vs. each label field | Do unsupervised stem groups match the DJ's categories? |
| 5 | Element → expected-stem check | Does separation isolate what the DJ hears? |

Expected mapping for #5:

| Element | Expected stem |
|---------|---------------|
| 1 Bass, 4 Low pads | bass |
| 3 Latin perc/congas, 7 Tech-house hi-hats | drums |
| 2 Pads, 6 Acid, 8 Minimal | other (acid may bleed into bass) |
| 5 Vocals | vocals |
| 9 Intense techno | drums + other |

### Output
- **Stem weight vector per label field** (with fold-level confidence intervals)
- Final model = mix + weighted stems, kept per field only where it beats the mix-only model
- `reports/stem_value.md` + UI page (EPIC-6)

---

## Technical Context

```python
# Leave-one-stem-out ablation (sketch)
sources = ["mix", "drums", "bass", "other", "vocals"]
full = score(concat([X[s] for s in sources]), y, folds)
for s in sources[1:]:
    ablated = score(concat([X[t] for t in sources if t != s]), y, folds)
    unique_value[s] = full - ablated
```

Measured per field because stems likely matter for different things (drums → intensity/congas; other → style/pads; bass → bass/low pads).

---

## Acceptance Criteria

- [ ] Stem value table per label field with confidence intervals across folds
- [ ] Stem-weighted model compared with the mix-only model per field; adopted only where it helps
- [ ] Stem clusters listened to and named (at least drums, bass, other)
- [ ] Report states which stem matters most for each part of the code, with evidence

---

## Key Technical Decisions

- Fusion method for the final model: weighted concatenation vs. stacking
- Whether to separate only a representative section (inherits EPIC-4 decision)

---

## Downstream Epics

- **EPIC-7b:** tag writing uses the final model
- **EPIC-8:** stem-weighted similarity
