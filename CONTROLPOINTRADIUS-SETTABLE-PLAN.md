# Make ControlPointRadius Settable in LocationPolygonView (Updated for PointSetView)

## Overview
The `LocationPolygonView` class has been refactored to use `PointSetView` instead of an array of `CircleView` objects for displaying control points. This plan documents how the `ControlPointRadius` property is settable and integrates with the new `PointSetView` implementation.

## Current Implementation

### Architecture
- `LocationPolygonView` uses a single `PointSetView ControlPointView` instance (replacing `CircleView[] ControlPointViews`)
- `PointSetView` internally manages `CircleView[] PointViews` for rendering
- Control points are created from polygon vertices via `GetAllPolygonVertices()` method
- Control point radius comes from `Global.AnnotationSettings.PolygonPointRadius` by default
- Color is `HSLColor.AdjustHSLHue(180, 0.5f)`

### ControlPointRadius Property

**File:** `Clients/Viking/WebAnnotation/View/LocationPolygonView.cs`

The `ControlPointRadius` property is now settable:

```csharp
private double _ControlPointRadius;

public double ControlPointRadius
{
    get => _ControlPointRadius;
    set
    {
        if (Math.Abs(_ControlPointRadius - value) > 0.01)
        {
            _ControlPointRadius = value;
            if (ControlPointView != null)
            {
                ControlPointView.PointRadius = value;
                ControlPointView.UpdateViews();
            }
        }
    }
}
```

### Key Features

1. **Default Value**: Initialized from `Global.AnnotationSettings.PolygonPointRadius` in constructor (line 117)
   - This value is the user's saved preference from the annotation preferences dialog
   - The same setting can be changed from the annotation preferences page

2. **Settable Property**: The property can be set programmatically, and changes automatically update:
   - The internal `_ControlPointRadius` field
   - The `PointSetView.PointRadius` property
   - The visual display via `ControlPointView.UpdateViews()`

3. **Integration with PointSetView**:
   - When `ControlPointRadius` is set, it updates `ControlPointView.PointRadius`
   - `PointSetView` internally regenerates its `CircleView[] PointViews` when `UpdateViews()` is called
   - The change threshold (0.01) prevents unnecessary updates for floating-point rounding

### Initialization Points

1. **Constructor** (lines 134-138):
   ```csharp
   ControlPointView = new PointSetView(HSLColor.AdjustHSLHue(180, 0.5f), Global.AnnotationSettings.PolygonPointRadius)
   {
       Points = GetAllPolygonVertices(VolumePolygon)
   };
   ControlPointView.UpdateViews();
   ```

2. **Initialize() Method** (lines 177-180):
   ```csharp
   ControlPointView.Points = GetAllPolygonVertices(VolumePolygon);
   ControlPointView.PointRadius = Global.AnnotationSettings.PolygonPointRadius;
   ControlPointView.UpdateViews();
   ```

### Property Updates

The `ControlPointRadius` property is also updated when related properties change:

- **Color Property** (lines 59-71): Updates `ControlPointView.Color` but doesn't change radius
- **Alpha Property** (lines 75-89): Updates `ControlPointView.Alpha` but doesn't change radius
- **BoundingBox Property** (line 265): Uses `ControlPointRadius` property (not the field) for bounding box calculations

### Usage Example

```csharp
// Get current radius
double currentRadius = polygonView.ControlPointRadius;

// Set new radius (automatically updates display)
polygonView.ControlPointRadius = 10.0;

// Set to user preference
polygonView.ControlPointRadius = Global.AnnotationSettings.PolygonPointRadius;
```

## User Preference Integration

The default control point radius is tied to the user's saved preference:

1. **Storage**: `Properties.Settings.Default.PolygonPointRadius` (via `Global.AnnotationSettings.PolygonPointRadius`)
2. **UI**: Annotation Preferences Dialog (`AnnotationPreferencesDialog.xaml`)
3. **Range**: 1.0 to 50.0 pixels (as defined in `AnnotationPreferencesDialogViewModel.cs`)
4. **Persistence**: Saved to user settings when changed in preferences dialog

## Benefits of PointSetView Approach

1. **Consolidated Management**: Single `PointSetView` instance manages all control points instead of array
2. **Automatic Updates**: `PointSetView.UpdateViews()` handles regeneration of underlying `CircleView[]` 
3. **Consistent API**: `PointRadius` property matches the pattern used in other view classes
4. **Cleaner Code**: Eliminates need for manual `CreateControlPointViews()` array creation

## Notes

- The `ControlPointRadius` property defaults to `Global.AnnotationSettings.PolygonPointRadius` which reflects the user's saved preference
- Changes to the property automatically propagate to the visual display through `PointSetView`
- The property uses a threshold check (0.01) to avoid unnecessary updates from floating-point precision issues
- The `BoundingBox` property uses `ControlPointRadius` to account for control point circles that fall outside the polygon
