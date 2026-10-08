# SAM2 grounding — reference

Read from [SKILL.md](SKILL.md) only when doing a theory pass or building an API map.

## Theory sources

Use the **pinned SHA**, not `main`.

| Source | Role |
|--------|------|
| SAM 2 paper (arXiv / linked from repo README at pin) | Architecture, multimask, memory attention (video), training objectives |
| `README.md` at pin | Supported workflows, checkpoints, install |
| Notebooks / demos at pin | Intended prompt patterns (points, boxes, masks) |
| Model / predictor source at pin | Exact encode→decode dataflow and state |

### Theory checklist (math / prompting designs)

- [ ] Image path: encoder embeddings vs prompt encoding vs mask decoder outputs (masks, IoU / scores, low-res logits)
- [ ] When multimask (typically ambiguous clicks) vs single-mask is appropriate
- [ ] Cost model: `set_image` / encoder vs repeated `predict`
- [ ] Video / memory only if the plan actually needs temporal tracking (SegmentationServer is image/tile oriented today)
- [ ] Checkpoint format: Meta `build_sam2` payload vs trainer raw state dict
- [ ] Any threshold or selection rule justified relative to SAM2 scores/logits, not invented absolute magic without evidence

## API map

### How to build the map

1. Resolve `SAM2_GIT_SHA` from `Servers/SegmentationServer/Dockerfile`.
2. List packages under `sam2/` at that commit (exclude tests unless needed).
3. For planning, fill this table from **pinned source** (signatures only; do not paste large code into plans):

| Symbol | Module | Purpose | Stateful? | Notes |
|--------|--------|---------|-----------|-------|
| `build_sam2` | `sam2.build_sam` | Construct model from Hydra config + ckpt | No | Config name + checkpoint path |
| `SAM2ImagePredictor` | `sam2.sam2_image_predictor` | Image encode + prompt predict | Yes (`set_image`) | Primary server path |
| … | … | … | … | Add video / AMG / helpers if the plan needs them |

4. For each method the plan calls (`set_image`, `predict`, reset/embedding export, etc.), confirm parameter names and defaults at the pin.
5. Scan sibling APIs the plan might use instead of a custom reimplementation.

### Viking call sites (wrapper, not Meta)

| Area | Path |
|------|------|
| Build + predictors | `Servers/SegmentationServer/segmentation_server/segmentation_server/segmentation_service.py` |
| Compile / Hydra overrides | `.../compile_config.py` |
| Tile growth / seams | `tile_growth.py`, `seams.py`, `mask_utils.py` |
| gRPC surface | `gRPC_Protos/Segmentation/SAM2/segmentation.proto` |
| Pin sync test | `segmentation_server/tests/test_stub_sync.py` |
| Operator docs | `Servers/SegmentationServer/README.md` |

### Trainer / export

| Area | Notes |
|------|------|
| Pin | `SAM2_GIT_SHA` in trainer docker/env (must match server unless deliberate) |
| Serve export | Meta-format checkpoint via `sam2-em-export-serve` (not raw `best_model.pt`) |
| Fine-tune plan | `docs/SAM2 Fine-Tuning Plan for TEM SEM Electron Microscopy.md` (product notes; still verify API against pin) |

## Anti-patterns

- Citing `main` docs or another fork’s API for this codebase
- Treating Viking seam/tile heuristics as SAM2 paper behavior
- Planning video-memory features without reading the video predictor at the pin
- Loading trainer raw state dicts with `build_sam2` without export conversion
- Silently changing `SAM2_GIT_SHA`
