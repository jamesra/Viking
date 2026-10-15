---
name: Segmentation back button
overview: Add a Back button on segmentation selection, rename volume Cancel to Back, and map mouse XButton1 (browser Back) to CancelCommand on both stages.
todos:
  - id: seg-back-button
    content: Add Back button bound to CancelCommand on SegmentationServiceSelectionControl.xaml (Back | Select | None)
    status: completed
  - id: volume-back-label
    content: Rename VolumeSelectionControl Cancel button Content to Back (still CancelCommand / IsCancel)
    status: completed
  - id: mouse-xbutton-back
    content: Handle PreviewMouseDown XButton1 on LoginWindow to invoke current stage CancelCommand
    status: completed
isProject: false
---

# Add Back on segmentation selection (UI + mouse)

## Situation

Login wizard stages: Login → Volume → Segmentation ([`LoginWindow.xaml`](Clients/Viking.UI.WPF/LoginWindow.xaml)).

Existing plumbing:

- Volume [`CancelCommand`](Clients/Viking.UI.WPF/ViewModels/VolumeSelectionViewModel.cs) → `SelectionCancelled` → login stage
- Segmentation [`CancelCommand`](Clients/Viking.UI.WPF/ViewModels/SegmentationServiceSelectionViewModel.cs) → `SelectionCancelled` → volume stage ([`OnSegmentationSelectionCancelled`](Clients/Viking.UI.WPF/LoginWindow.xaml.cs))

Gaps: segmentation has no Back button (only Select / None); volume button says Cancel; neither stage handles the mouse **Back** button (`MouseButton.XButton1`).

## Changes

### 1. Segmentation Back button

In [`SegmentationServiceSelectionControl.xaml`](Clients/Viking.UI.WPF/Controls/SegmentationServiceSelectionControl.xaml) button row (~189–215):

- Add **Back** — `Command="{Binding CancelCommand}"`, `IsCancel="True"`, same size as Select/None
- Order: **Back | Select | None**

### 2. Volume button label

In [`VolumeSelectionControl.xaml`](Clients/Viking.UI.WPF/Controls/VolumeSelectionControl.xaml): rename Cancel button `Content` to **Back** (keep `CancelCommand` and `IsCancel="True"`). Behavior unchanged (returns to login).

### 3. Mouse Back (XButton1) on both stages

Handle once on [`LoginWindow`](Clients/Viking.UI.WPF/LoginWindow.xaml.cs) so focus inside TreeView/ListBox still works:

- Subscribe `PreviewMouseDown` in the window constructor (or XAML)
- When `e.ChangedButton == MouseButton.XButton1`:
  - `VolumeSelection` → if `_volumeSelectionViewModel.CancelCommand.CanExecute(null)`, execute it
  - `SegmentationServiceSelection` → same for `_segmentationServiceSelectionViewModel.CancelCommand`
  - `Login` → ignore
- Set `e.Handled = true` when acted on

Do not use browser `NavigationCommands.BrowseBack` (not wired for mouse XButtons in a normal Window without a Frame).

## Verify

- Segmentation **Back** / Escape → volume list
- Volume **Back** / Escape → login
- Mouse XButton1 on volume and segmentation screens matches the Back button
- Select / None still complete login as today
