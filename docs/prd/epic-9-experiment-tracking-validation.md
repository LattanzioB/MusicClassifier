# EPIC-9: Experiment Tracking & Validation

> **Priority:** P1
> **Estimated Effort:** Medium
> **Phase:** Phase 1-2 (and used by every later phase)
> **Dependencies:** EPIC-5 (EPIC-6 for display)
> **Can Start After:** EPIC-5
> **Sprint:** 2-3
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §11 (Rev. 2)

---

## Goal

Make every experiment comparable — same folds, same metrics, logged runs — and provide listening tools for qualitative checks.

---

## Scope

### Quantitative
- Fixed fold assignment (grouped by `track_id`, stratified on genre+style) computed once and stored
- Run log in SQLite: config (embedding model, pooling, feature set, model params, sources), metrics per label field and fold, timestamp, git commit
- Comparison of runs per label field (table + chart)
- Learning curves: score vs. number of training labels per field → where more labels help most
- Confidence intervals across folds

### Qualitative
- Cluster-center sampling and boundary sampling for listening
- Mislabel candidates (confident predictions that disagree with the manual code)
- Low-confidence review list (feeds the EPIC-6 review queue)

### Optional
- MLflow if the custom run log becomes limiting

---

## Technical Context

| Method | Measures | When |
|--------|----------|------|
| Grouped CV vs. manual labels | Macro-F1 / MAE per field | Every configuration |
| Learning curves | Value of more labels | After baseline |
| Cluster–label agreement | AMI / ARI per field | Clustering runs |
| Internal metrics | Silhouette, DBCV | Tuning UMAP/HDBSCAN |
| Listening samples | Perceived coherence | After each notable run |

---

## Acceptance Criteria

- [ ] Every CLI training/clustering run logged with config and metrics
- [ ] Two runs comparable per label field in one view
- [ ] Learning curves available per field
- [ ] Test guarantees no `track_id` appears in both train and test of any fold

---

## Key Technical Decisions

- Custom SQLite log (default) vs. MLflow
- Stratification key for folds (genre+style proposed)

---

## Downstream Epics

- **EPIC-10:** Stem Value Analysis (reuses folds and run log)
