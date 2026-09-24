# EPIC-6: Streamlit Experimentation UI

> **Priority:** P0 (parallel track)
> **Estimated Effort:** Large
> **Phase:** Parallel — grows each phase
> **Dependencies:** EPIC-1, EPIC-2; consumes EPIC-3, 5, 9, 10
> **Can Start After:** EPIC-2
> **Sprint:** 3 (core) · 4 (stem value page)
> **Source:** [epic-backlog.md](../epic-backlog.md) | [technical-analysis.md](../technical-analysis.md) §4 (Rev. 2)

---

## Goal

A workbench to browse the library by the DJ's code, listen, review predictions, compare runs, and see the stem value report. **Pipeline jobs run from the CLI; the UI reads their results.**

---

## Scope

### Track browser
- Columns: artist, title, BPM, key, manual code, predicted code, confidence, cluster
- Filters on every label field: genre, style, intensity range, each element (absent/present/intense), descriptors, "has manual label", "held-out folder"
- Inline audio snippet taken from the main section (not the intro)

### Cluster view
- 2D UMAP scatter (Plotly), colored by any label field or cluster; hover info; select → play

### Review queue
- Least-confident and manual-vs-predicted disagreements first
- Accept / correct → stored as `review_ui` labels (DB only; audio files untouched)

### Label health
- Counts per class; rare classes needing examples (elements 7/8, deep, trance, hype)

### Runs
- Metrics per label field for selected runs, side by side

### Stem value (after EPIC-10)
- Stem weight table/chart per label field; stem cluster browser

---

## Technical Context

- Streamlit reads SQLite + cached arrays; `st.cache_data` for embeddings/UMAP coordinates
- Long-running jobs are **not** triggered from Streamlit (fragile with reruns); the UI shows the CLI's latest runs
- Audio: slice from the 16 kHz cache or the original file; `st.audio` with numpy/bytes
- Alternatives for pure exploration: Renumics Spotlight, marimo

---

## Acceptance Criteria

- [ ] Table filters by every label field and plays audio inline
- [ ] Review queue stores corrections as new labels (never touching audio files)
- [ ] Cluster scatter renders ~3,500 points interactively
- [ ] Runs comparable side by side

---

## Key Technical Decisions

- `st.dataframe` vs. AgGrid
- Snippet source: decoded cache vs. original file

---

## Downstream Epics

- **EPIC-9:** display of experiment comparisons
- **EPIC-10:** stem value page
