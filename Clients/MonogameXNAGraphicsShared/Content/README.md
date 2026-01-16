# MonogameXNAGraphicsShared Content

## Purpose

This folder contains **ONLY shared shader helper files and common assets** used by both graphics projects:
- **VikingXNAGraphics** (net48, MonoGame 3.7.1)
- **Monographics** (net9.0, MonoGame 3.8.5-develop.13)

## Structure

### Shared Shader Helper Files (.fx)

These files contain shader functions, structures, and constants that are included by platform-specific technique files:

- `LineCurveCommon.fx` - Common constants and helper functions for lines and curves
- `HSLRGBLib.fx` - Color space conversion utilities (HCL/RGB)
- `LineCurvePixelShaderShared.fx` - Shared pixel shader structures and samplers
- `OverlayShaderShared.fx` - Shared overlay shader samplers and variables
- `LineVertexShader.fx` - Line vertex shader function (no techniques)
- `CurveVertexShader.fx` - Curve vertex shader function (no techniques)
- `LineCurvePixelShaders.fx` - Line/curve pixel shader functions
- `LineCurveHSVPixelShaders.fx` - HSV-based pixel shader functions

### Shared Assets

- `*.png` - Image assets (Circle, CircleChain, CircleConnect, etc.)
- `Arial.spritefont` - Font definition file

## Platform-Specific Technique Files

Platform-specific technique files (those with `technique` blocks) are located in:

- **VikingXNAGraphics/Content/** - net48 project, Windows/DirectX, vs_4_0/ps_4_0
- **Monographics/Content/** - net9.0 project, Windows/DirectX, vs_4_0/ps_4_0

These files include the shared helpers using:
```hlsl
#include "../../MonogameXNAGraphicsShared/Content/SharedFile.fx"
```

## Build Process

### VikingXNAGraphics (net48)
- Uses `VikingXNAGraphics/Content/Content.mgcb`
- Platform: Windows
- Shader profiles: vs_4_0, ps_4_0
- MonoGame 3.7.1.189

### Monographics (net9.0)
- Uses `Monographics/Content/Content.mgcb`
- Platform: Windows
- Shader profiles: vs_4_0, ps_4_0
- MonoGame 3.8.5-develop.13 (WindowsDX)

Both projects reference shared assets from this folder via relative paths in their Content.mgcb files.

## Maintenance

- **Shared logic changes**: Update helper files in this folder
- **Platform-specific changes**: Update technique files in respective project Content folders
- **New assets**: Add to this folder and reference in both project Content.mgcb files
- **Do not create** technique blocks in these shared files - they must remain include-only

## Notes

- XNA is obsolete - all projects now use MonoGame
- DesktopVK/DesktopGL platforms are not used due to shader compatibility issues
- Both projects use the same shader profiles (vs_4_0/ps_4_0) but different MonoGame versions









