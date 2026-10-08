---
name: sam2-grounding
description: Grounds SAM2 (Meta Segment Anything 2) plans in pinned-commit theory and a complete API map before changing SegmentationServer or related inference. Use when planning or editing SAM2 integration, set_image/predict, multimask, embeddings, checkpoints, Hydra configs, compile, fine-tune export, or any Meta sam2 API usage.
---

# SAM2 grounding (theory + API)

Canonical skill for Meta SAM2 in Viking. **Do not invent APIs from memory. Do not treat `main` as the API.** Plans that change inference math, prompting, caching, or model I/O must ground here first.

Pin ownership and notify rules: [sam2-pin.mdc](../../rules/sam2-pin.mdc).

## Before planning or coding

Copy and track:

```
SAM2 grounding:
- [ ] Read pin SHA from Servers/SegmentationServer/Dockerfile (`ARG SAM2_GIT_SHA=…`)
- [ ] Confirm trainer pin matches (Sam2SegmentationTrainer `SAM2_GIT_SHA` / docker env) or note deliberate drift
- [ ] Theory pass (paper + architecture notes at that SHA) for any math / prompting / memory / loss design
- [ ] API map at that SHA for surfaces the plan will touch (and scan for unused better APIs)
- [ ] Separate Meta SAM2 behavior from Viking wrapper (gRPC, tiles, seams, TLS)
- [ ] Do not bump or unpin SHA unless James asks
```

## Resolve the pin

1. Read `SAM2_GIT_SHA` from `Servers/SegmentationServer/Dockerfile`.
2. Prefer a local clone at that commit if present (e.g. container `/home/user/segment-anything-2` or trainer `/opt/sam2`).
3. Otherwise fetch GitHub at **that SHA only**:
   - Tree: `https://github.com/facebookresearch/sam2/tree/<SHA>`
   - Raw file: `https://raw.githubusercontent.com/facebookresearch/sam2/<SHA>/<path>`
4. If `main` is newer than the pin, tell James; do not silently follow `main` (see pin rule).

## Theory (sound practice)

When the task involves prompting strategy, multimask selection, embedding reuse, video/memory, losses, or “how SAM2 should behave”:

1. Open the paper + README / notebooks **at the pinned SHA** (not a random blog, not unpinned `main`).
2. Map the claim to modules: image encoder (Hiera) → prompt encoder → mask decoder; video path adds memory if relevant.
3. Prefer designs that match Meta’s intended contracts (encode once / predict many; point/box/mask prompts; multimask vs single).
4. Keep Viking-specific heuristics (seams, tile growth, border cleanup) out of “SAM2 theory” — document them as wrapper policy.

Details and source list: [reference.md](reference.md#theory-sources).

## API map (efficient use)

When the task uses or could use Meta’s Python API:

1. Inventory public entry points at the pin (`build_sam2`, `SAM2ImagePredictor`, video predictor, automatic mask generator, Hydra YAMLs, checkpoint load format).
2. For each touched class/function, record signature, side effects (especially `set_image` state), and return values from **source at that SHA**.
3. Check whether the plan under-uses the API (e.g. multimask, mask inputs, batch helpers) before inventing a parallel path.
4. Never assume kwargs or methods from training memory — confirm in pinned source.

Inventory checklist and Viking call sites: [reference.md](reference.md#api-map).

## Three layers (do not conflate)

| Layer | Truth lives in |
|-------|----------------|
| Meta SAM2 theory + API | `facebookresearch/sam2` **at pin SHA** |
| Viking segmentation service | `Servers/SegmentationServer/` (README, `segmentation_service.py`, tile/seam modules, proto) |
| Fine-tune / export | Sam2SegmentationTrainer (`sam2-em-export-serve`, Meta `{"model": state_dict}` checkpoints) |

Primary Meta surface already used in-server: `sam2.build_sam.build_sam2`, `sam2.sam2_image_predictor.SAM2ImagePredictor` (`set_image` / `predict` + embedding state). Wrapper docs: `Servers/SegmentationServer/README.md`.

## Trainer repo

Agents working only in Sam2SegmentationTrainer should open that repo’s `.cursor/skills/sam2-grounding/SKILL.md`, which points here. Keep pins aligned unless James asks otherwise.
