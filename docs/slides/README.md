# TFM slide material

Everything needed to build and maintain the TFM presentation deck.

```
docs/slides/
  flows/           <- Playwright-generated screenshots of every E2E flow (source of truth)
  presentation/     <- the actual deck: outline, editable mermaid diagrams, the .pptx
```

## flows/

89 screenshots across 9 feature areas, captured by `Project/frontend/e2e/screenshots/*.spec.ts`
against the real running app — not hand-captured, so every image reflects actual UI state
(including error/validation paths, not just happy-path). See `flows/README.md` for how to
regenerate them.

## presentation/

- `outline.md` — the approved slide-by-slide plan (content, which images, which diagrams go where)
- `flows.md` — the mermaid source for every diagram used in the deck, kept editable outside the
  `.pptx` (diagrams are rendered to PNG and inserted as images — PowerPoint doesn't render mermaid
  natively)
- `MyBudget.pptx` — the deck itself

Regenerate diagram PNGs after editing `flows.md`, then re-embed in the `.pptx` — see
`presentation/outline.md` for the current build process.
