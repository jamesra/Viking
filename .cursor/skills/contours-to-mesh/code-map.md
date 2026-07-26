# Code map — contours to mesh

## Projects

| Project | Role |
|---------|------|
| `MorphologyMesh/` | Core Bajaj generator, slice graph, region graph, Collada serialization |
| `Geometry/` | Primitives, Delaunay, medial axis, meshing helpers |
| `Clients/MonogameTestbed/` | Visual testbed, BajajMultiTest, MeshTest, MeshAssemblyPlanner UI |
| `AnnotationVizLib*` | Load morphology graphs from OData/WCF |
| `Clients/VolumeModel/` | Volume transforms (mesh coords tie to volume space) |

## MorphologyMesh — key types

| File | Types / responsibility |
|------|------------------------|
| `MorphologyMesh/SliceGraph.cs` | Build slice graph from morphology graph; Z-adjacent slice nodes |
| `MorphologyMesh/SliceTopology.cs` | Upper/lower shape sets per slice; correspondence |
| `MorphologyMesh/ConcurrentTopologyInitializer.cs` | Parallel topology + `AddCorrespondingVerticies` |
| `MorphologyMesh/BajajGeneratorMesh.cs` | Per-slice 3D mesh: vertices, edges, faces, shapes |
| `MorphologyMesh/MorphMeshVertex.cs` | Position, `ShapeIndex`, corresponding-vertex flag |
| `MorphologyMesh/MorphMeshEdge.cs` | `EdgeType` (CONTOUR, SLICECHORD, …) |
| `MorphologyMesh/MorphMeshRegion.cs` | Region polygon, perimeter, Z levels |
| `MorphologyMesh/MorphMeshRegionGraph.cs` | Graph of regions for pairing/closing |
| `Generators/BajajMeshGenerator.cs` | `ConvertToMesh`, `GenerateFaces`, Delaunay, OTV, theorems |
| `RegionGraphExtensions.cs` | `MergeAndCloseRegionsPass`, `TryClosingUntiledRegion` |
| `SliceChord.cs` | Slice chord representation |
| `MorphMeshOutwardOrientation.cs` | Outward normal / winding checks |
| `Serialization/MorphologyColladaView.cs` | Export |

## MonogameTestbed — entry points

| File | Role |
|------|------|
| `BajajMultiTest.cs` | Full-cell mesh from OData graph; `SliceGraph.Create` → `BajajMeshGenerator.ConvertToMesh` |
| `MeshTest.cs` | Single-case / repro mesh tests |
| `MeshAssemblyPlanner.cs` | Binary-tree parallel compositing |
| `Views/SliceGraphMeshModel.cs` | Merge slice meshes via `ShapeIndex` / `PointIndex` |
| `BoundaryFinder.cs` | Boundary surface helpers |

## Geometry dependencies

| File | Role |
|------|------|
| `Geometry/Algorithms/Delaunay.cs` | Divide-and-conquer Delaunay (fragile on degenerate input) |
| `Geometry/Algorithms/MedialAxis.cs` | `MedialAxisFinder.ApproximateMedialAxis` for untiled regions |
| `Geometry/Meshing/MeshExtensions.cs` | Triangulation, `CleanRegionTriangulationInput` |
| `Geometry/Primitives/GridPolygon.cs` | `AddPointsAtIntersections`, contour ops |

## `GenerateFaces` call graph (simplified)

```
BajajMeshGenerator.GenerateFaces(mesh)
├── AddDelaunayEdges(mesh)
├── GenerateRegionGraph(mesh)
├── mesh.RemoveInvalidEdges()
├── CompleteCorrespondingVertexFaces(mesh)
├── RegionPairingGraph.MergeAndCloseRegionsPass(mesh, rTree)     // pass 1 untiled
├── FirstPassSliceChordGeneration(mesh, …)
├── FirstPassFaceGeneration(mesh)
├── SecondPassRegionDetection(mesh, incompleteVerts)
│   └── MergeAndCloseRegionsPass(mesh, rTree)                    // pass 2
├── FirstPassFaceGeneration(mesh)
├── CapMeshEnd (if open slice end)
└── EnsureFacesHaveExternalNormals / RecalculateNormals
```

## Enums to know

**`SliceChordTestType`** (`BajajMeshGenerator.cs`): `ChordIntersection`, `Theorem2`, `Theorem4`, `LineOrientation`, `EdgeType`, `Correspondance`, … — combined in `PassCriteria` arrays for progressively looser chord passes.

**`RegionType`**: includes `UNTILED` — regions closed via medial axis, not OTV pairing.

**`EdgeType`**: classifies Delaunay/triangulation edges (internal, flying, valid cross-contour, etc.).

## Common failure signatures

| Symptom | Likely stage |
|---------|----------------|
| `could not add corresponding point` | `SliceGraph` / `AddPointsAtIntersections` |
| `Can't create line with two identical points` | Untiled triangulation input (`TryClosingUntiledRegion`) |
| `EdgesIntersectTriangulationException` | Degenerate Delaunay input (colinear stacks) |
| `Exception building mesh U: … D: …` | Pass 2 catch in `GenerateFaces` |
| Missing faces, partial mesh | Failed untiled or slice-chord pass (non-fatal) |
| Wrong normals / inverted lighting | Winding — `MorphMeshOutwardOrientation` |

## Docs & papers

- `Documentation/source/developerdocs/mesh/overview.rst` — illustrated pipeline
- `MorphologyMesh/Papers/*.pdf` — primary references
- `.cursor/skills/contours-to-mesh/*-extract.txt` — full-text extracts for search
