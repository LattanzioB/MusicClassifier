# EPIC-5: Classification & Clustering Engine (Stage 3)

> **Priority:** P0
> **Estimated Effort:** Large
> **Phase:** Phase 1-2
> **Dependencies:** EPIC-3 (full-mix embeddings), EPIC-2 (labels)
> **Can Start After:** EPIC-3
> **Sprint:** 2-3
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §9 (Rev. 2)

---

## Goal

Learn the DJ's code from the embeddings — one model per label field — with honest grouped cross-validation, and use clustering for discovery and mislabel detection.

---

## Scope

### Supervised label models

| Field | Labeled (full format) | Task | Metric |
|-------|------------------------|------|--------|
| Genre | deep 42 · prog 1,805 · techno 272 · trance 24 | Multi-class, balanced weights | Macro-F1, per-class recall |
| Style | deep 188 · fly 1,419 · groovy 471 · hype 65 | Multi-class | Macro-F1 |
| Intensity | 1: 101 · 2: 740 · 3: 983 · 4: 319 | Ordinal regression (+ low-level features) | MAE, Spearman, ±1 accuracy |
| Elements 1–6, 9 | see technical-analysis §2.4 | absent/present/intense per element | Macro-F1; AUC for present |
| Elements 7, 8 | 34 / 17 positives | **Not trained** until ≥ 30 positives | — |
| Descriptors | ~15 words with ≥ 30 uses | Binary per word; structural ones (`ctd`, `hungup`, `bigdrop`) with per-window features | F1 / AP |

- `StratifiedGroupKFold` (5 folds, groups = `track_id`); fold assignment fixed and stored (EPIC-9)
- Held-out folder tracks excluded from training and CV; predicted at the end as an unseen test
- Per-track confidence; disagreements with manual labels → mislabel candidates
- Predictions for unlabeled / partially labeled tracks assembled into the DJ's code format (via `labels/formatter.py`)
- Feature configurations: EffNet · MAEST · + Discogs-400 · + low-level; pooling strategy

### Clustering (discovery)

- UMAP + `sklearn.cluster.HDBSCAN`, configurable, saved per run
- AMI/ARI vs. each label field; silhouette, DBCV
- 2D UMAP coordinates for the UI

---

## Technical Context

```python
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import StratifiedGroupKFold, cross_val_predict
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

model = make_pipeline(StandardScaler(), LogisticRegression(max_iter=2000, class_weight="balanced"))
cv = StratifiedGroupKFold(n_splits=5, shuffle=True, random_state=0)
proba = cross_val_predict(model, X, y, groups=track_ids, cv=cv, method="predict_proba")
```

```python
import umap
from sklearn.cluster import HDBSCAN

reduced = umap.UMAP(n_neighbors=30, min_dist=0.0, n_components=15, metric="cosine").fit_transform(X)
labels = HDBSCAN(min_cluster_size=30, min_samples=10).fit_predict(reduced)
```

| UMAP purpose | n_neighbors | min_dist | n_components |
|--------------|-------------|----------|--------------|
| Clustering | 30-50 | 0.0 | 10-15 |
| 2D visualization | 15 | 0.1 | 2 |

**Why supervised first:** clusters don't come with names or a 1–4 intensity scale; ~2,100 labeled tracks give a direct, measurable way to reproduce the DJ's code. Clustering remains for finding sub-styles, mislabels and new descriptors.

**Imbalance:** 84% of tracks are progressive. Macro-F1 (not accuracy) is the headline metric; deep/trance/hype get targeted labeling through the review queue.

---

## Acceptance Criteria

- [ ] Baseline scores recorded for every label field with grouped CV
- [ ] Predictions + confidence stored per run for every track
- [ ] Training + CV for all fields < 2 minutes
- [ ] Clustering runs, parameters and metrics saved and comparable

---

## Key Technical Decisions

- Ordinal vs. two-binary formulation for element levels (by score)
- Uncertainty threshold for reports
- Whether partially labeled tracks contribute to fields they have (default: yes)

---

## Downstream Epics

- **EPIC-7:** Prediction Report & Tag Export
- **EPIC-9:** Experiment Tracking
- **EPIC-10:** Stem Value Analysis (reuses the same models on stem features)
