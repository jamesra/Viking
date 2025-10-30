# Async ODataSpatialDataFactory Creation Plan

## Current State

**ODataSpatialDataFactory does NOT exist yet** in `AnnotationVizLibODataClient`. This is a net-new implementation based on:
- `SimpleODataSpatialDataFactory` (inspiration/reference implementation)
- Patterns established in the recently refactored `ODataNeuronFactory`

## Reference Implementation Analysis

The `SimpleODataSpatialDataFactory` has several issues that we should **NOT** replicate:

### Blocking Operations (Lines 39, 49, 62, 171, 179)
- `.Wait()` calls on async tasks throughout
- Blocks threads during I/O operations
- No true async benefit despite using Task API

### Sequential Processing Anti-Patterns
- Lines 37-53: Pagination handled synchronously with blocking .Wait() between pages
- Lines 165-183: Similar pagination pattern with blocking waits
- Could fetch multiple pages in parallel but doesn't

### Missing Features
- No `CancellationToken` support in any method
- No async method variants (all methods are synchronous with blocking waits on tasks)
- No error handling or retry logic
- No batch optimization

### Inefficient Dictionary Patterns
- Line 106-107: `ContainsKey` check + `Add` instead of `TryAdd`
- Line 142-144: `ContainsKey` check before adding to dictionary
- Line 210-211: `ContainsKey` check + indexing (double lookup)

### Good Patterns to Adopt
✅ Building lookup maps (ChildToParent, ChildToEdge)
✅ Separation of concerns (node vs edge spatial data)
✅ Pagination handling structure
✅ Dictionary-based data appending
✅ Using `TryGetValue` for node lookups (line 79)

## Implementation Plan

### 1. Create Base File Structure

**New File: `AnnotationVizLibODataClient\ODataSpatialDataFactory.cs`**

```csharp
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnnotationVizLib.OData
{
    public static class ODataSpatialDataFactory
    {
        // Implementation here
    }
}
```

### 2. Implement Public Synchronous API

Main entry point for backward compatibility:

```csharp
/// <summary>
/// Appends spatial data to a neuron graph from OData service
/// </summary>
public static void AppendSpatialDataFromOData(
    NeuronGraph graph, 
    Uri Endpoint, 
    ICollection<long> IDs, 
    uint Hops)
{
    AppendSpatialDataFromODataAsync(graph, Endpoint, IDs, Hops)
        .GetAwaiter().GetResult();
}
```

### 3. Implement Main Async Method

**Method: `AppendSpatialDataFromODataAsync`**
```csharp
public static async Task AppendSpatialDataFromODataAsync(
    NeuronGraph graph,
    Uri Endpoint,
    ICollection<long> IDs,
    uint Hops,
    CancellationToken cancellationToken = default)
{
    Container container = new Container(Endpoint)
    {
        MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
    };

    // Fetch both in parallel
    var neuronDataTask = AppendNeuronSpatialDataAsync(
        graph, container, IDs, Hops, cancellationToken);
    var edgeDataTask = AppendAreaToConnectionsAsync(
        graph, container, IDs, Hops, cancellationToken);

    await Task.WhenAll(neuronDataTask, edgeDataTask);
}
```

### 4. Implement Neuron Spatial Data Loading

**Method: `AppendNeuronSpatialDataAsync`**
```csharp
private static async Task AppendNeuronSpatialDataAsync(
    NeuronGraph graph,
    Container container,
    ICollection<long> IDs,
    uint Hops,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Build query for network spatial data
        var spatialCacheQuery = (IDs == null || IDs.Count == 0) && graph.Nodes.Count > 0
            ? container.NetworkSpatialData(new List<long>(), 0)
            : container.NetworkSpatialData(IDs, (int)Hops);

        // Fetch all spatial cache data
        var spatialCaches = await spatialCacheQuery
            .GetAllPagesToListAsync(cancellationToken);

        // Append to graph nodes
        foreach (var cache in spatialCaches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (graph.Nodes.TryGetValue((ulong)cache.ID, out var node))
            {
                AppendSpatialCacheToNode(node, cache);
            }
        }
    }
    catch (Exception ex) when (!(ex is OperationCanceledException))
    {
        throw new InvalidOperationException(
            $"Failed to append neuron spatial data: {ex.Message}", ex);
    }
}
```

**Method: `AppendNeuronSpatialDataAsync` (single node overload)**
```csharp
public static async Task AppendNeuronSpatialDataAsync(
    NeuronNode node,
    Container container,
    CancellationToken cancellationToken = default)
{
    try
    {
        var spatialCache = await container.StructureSpatialCaches
            .Where(s => s.ID == node.Key)
            .GetAllPagesToListAsync(cancellationToken);

        var cache = spatialCache.FirstOrDefault();
        if (cache != null)
        {
            AppendSpatialCacheToNode(node, cache);
        }
    }
    catch (Exception ex) when (!(ex is OperationCanceledException))
    {
        throw new InvalidOperationException(
            $"Failed to append spatial data for node {node.Key}: {ex.Message}", ex);
    }
}
```

### 5. Implement Edge Spatial Data Loading

**Method: `AppendAreaToConnectionsAsync`**
```csharp
private static async Task AppendAreaToConnectionsAsync(
    NeuronGraph graph,
    Container container,
    ICollection<long> IDs,
    uint Hops,
    CancellationToken cancellationToken = default)
{
    try
    {
        // Build lookup maps
        var childToEdgeMap = BuildChildToEdgeMap(graph);

        // Build query for network edge spatial data
        var edgeSpatialQuery = (IDs == null || IDs.Count == 0) && graph.Nodes.Count > 0
            ? container.NetworkEdgeSpatialData(new List<long>(), 0)
            : container.NetworkEdgeSpatialData(IDs, (int)Hops);

        // Fetch all edge spatial cache data
        var edgeSpatialCaches = await edgeSpatialQuery
            .GetAllPagesToListAsync(cancellationToken);

        // Append to graph edges
        foreach (var cache in edgeSpatialCaches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            AddSpatialDataToEdges(graph, cache, childToEdgeMap);
        }
    }
    catch (Exception ex) when (!(ex is OperationCanceledException))
    {
        throw new InvalidOperationException(
            $"Failed to append edge spatial data: {ex.Message}", ex);
    }
}
```

### 6. Implement Helper Methods for Data Appending

**Method: `AppendSpatialCacheToNode`**
```csharp
private static void AppendSpatialCacheToNode(
    NeuronNode node, 
    StructureSpatialCache cache)
{
    // Map StructureSpatialCache properties to node attributes
    if (cache.Area.HasValue)
        node.Attributes["Area"] = cache.Area.Value;
    
    if (cache.MinZ.HasValue)
        node.Attributes["MinZ"] = cache.MinZ.Value;
    
    if (cache.MaxZ.HasValue)
        node.Attributes["MaxZ"] = cache.MaxZ.Value;
    
    if (cache.Centroid != null)
        node.Attributes["Centroid"] = cache.Centroid;
    
    if (cache.ConvexHull != null)
        node.Attributes["ConvexHull"] = cache.ConvexHull;
    
    // Add any other properties available in StructureSpatialCache
}
```

**Method: `AddSpatialDataToEdges`**
```csharp
private static void AddSpatialDataToEdges(
    NeuronGraph graph,
    StructureSpatialCache childCache,
    Dictionary<ulong, SortedSet<NeuronEdge>> childToEdgeMap)
{
    ulong childKey = (ulong)childCache.ID;

    if (!childToEdgeMap.TryGetValue(childKey, out var edges))
        return;

    // Add area data
    if (childCache.Area.HasValue)
    {
        double area = childCache.Area.Value;

        foreach (var edge in edges.Where(e => e.SourceIDs.Contains(childKey)))
        {
            edge.TotalSourceArea += area;
        }

        foreach (var edge in edges.Where(e => e.TargetIDs.Contains(childKey)))
        {
            edge.TotalTargetArea += area;
        }
    }

    // Add Z range data
    if (childCache.MinZ.HasValue && childCache.MaxZ.HasValue)
    {
        double minZ = childCache.MinZ.Value;
        double maxZ = childCache.MaxZ.Value;

        foreach (var edge in edges.Where(e => e.SourceIDs.Contains(childKey)))
        {
            if (edge.MinZ > minZ)
                edge.MinZ = minZ;

            if (edge.MaxZ < maxZ)
                edge.MaxZ = maxZ;
        }
    }
}
```

### 7. Implement Lookup Map Builders

**Method: `BuildChildToParentMap`**
```csharp
private static Dictionary<ulong, long> BuildChildToParentMap(NeuronGraph graph)
{
    var childToParent = new Dictionary<ulong, long>();

    foreach (NeuronNode node in graph.Nodes.Values)
    {
        foreach (ulong childID in node.EdgeSourceChildStructureIDs)
        {
            childToParent.TryAdd(childID, node.Key);
        }

        foreach (ulong childID in node.EdgeTargetChildStructureIDs)
        {
            childToParent.TryAdd(childID, node.Key);
        }
    }

    return childToParent;
}
```

**Method: `BuildChildToEdgeMap`**
```csharp
private static Dictionary<ulong, SortedSet<NeuronEdge>> BuildChildToEdgeMap(
    NeuronGraph graph)
{
    var idToEdge = new Dictionary<ulong, SortedSet<NeuronEdge>>();

    foreach (NeuronEdge edge in graph.Edges.Values)
    {
        foreach (ulong sourceID in edge.SourceIDs)
        {
            if (!idToEdge.TryGetValue(sourceID, out var edgeSet))
            {
                edgeSet = new SortedSet<NeuronEdge>();
                idToEdge[sourceID] = edgeSet;
            }
            edgeSet.Add(edge);
        }

        foreach (ulong targetID in edge.TargetIDs)
        {
            if (!idToEdge.TryGetValue(targetID, out var edgeSet))
            {
                edgeSet = new SortedSet<NeuronEdge>();
                idToEdge[targetID] = edgeSet;
            }
            edgeSet.Add(edge);
        }
    }

    return idToEdge;
}
```

### 8. Add Synchronous Method Overloads for Single Node

**Method: `AppendNeuronSpatialData` (synchronous single node)**
```csharp
public static void AppendNeuronSpatialData(
    NeuronNode node, 
    Container container)
{
    AppendNeuronSpatialDataAsync(node, container)
        .GetAwaiter().GetResult();
}
```

### 9. Verify OData Container Functions

Ensure the following OData functions are available in the Container class:
- `NetworkSpatialData(ICollection<long> IDs, int Hops)` - Returns `DataServiceQuery<StructureSpatialCache>`
- `NetworkEdgeSpatialData(ICollection<long> IDs, int Hops)` - Returns `DataServiceQuery<StructureSpatialCache>`
- `StructureSpatialCaches` - Entity set property

If these don't exist, we need to check the OData service schema and possibly regenerate the client.

### 10. Error Handling Strategy

All async methods should:
```csharp
try
{
    // Async operation
}
catch (Exception ex) when (!(ex is OperationCanceledException))
{
    throw new InvalidOperationException(
        $"Context-specific error message: {ex.Message}", ex);
}
```

### 11. Optimization Opportunities

**Parallel Page Fetching** (advanced optimization):
Instead of fetching pages sequentially, could fetch multiple pages in parallel if we know the page count.

**Batch Processing**:
Group updates to avoid excessive graph traversal.

**Caching**:
Consider caching lookup maps if called multiple times for the same graph.

## Key Differences from SimpleODataSpatialDataFactory

| Aspect | SimpleOData (Old) | OData (New) |
|--------|------------------|-------------|
| **Async Pattern** | Fake (Task + .Wait()) | True async/await |
| **Cancellation** | None | CancellationToken throughout |
| **Data Fetching** | Sequential with blocking | Async with potential parallelism |
| **Pagination** | Manual with .Wait() loops | Handled by GetAllPagesToListAsync |
| **Dictionary Ops** | ContainsKey + Add/Index | TryGetValue, TryAdd |
| **Error Handling** | Debug.Assert only | Try-catch with meaningful messages |
| **Data Source** | Dictionary parsing | Strongly-typed OData entities |
| **Public API** | Sync only | Sync + Async variants |

## Implementation Checklist

### Phase 1: Core Infrastructure
- [ ] Create ODataSpatialDataFactory.cs file
- [ ] Add namespace and using statements
- [ ] Implement synchronous public entry point
- [ ] Implement main async method with parallel fetching

### Phase 2: Neuron Spatial Data
- [ ] Implement AppendNeuronSpatialDataAsync (graph overload)
- [ ] Implement AppendNeuronSpatialDataAsync (single node overload)
- [ ] Implement AppendSpatialCacheToNode helper
- [ ] Add synchronous wrapper for single node

### Phase 3: Edge Spatial Data
- [ ] Implement AppendAreaToConnectionsAsync
- [ ] Implement AddSpatialDataToEdges
- [ ] Build child-to-edge lookup map

### Phase 4: Helper Methods
- [ ] Implement BuildChildToParentMap
- [ ] Implement BuildChildToEdgeMap
- [ ] Optimize dictionary operations

### Phase 5: Error Handling & Testing
- [ ] Add try-catch blocks to all async methods
- [ ] Add cancellation token checks in loops
- [ ] Test with various graph sizes
- [ ] Test cancellation behavior
- [ ] Verify performance improvements

## Expected Benefits

1. **True Async**: No thread blocking during I/O operations
2. **Parallel Fetching**: Node and edge spatial data fetched simultaneously
3. **Cancellation Support**: Can cancel long-running operations
4. **Better Error Messages**: Context-aware exception messages
5. **Type Safety**: Uses strongly-typed OData entities vs dictionary parsing
6. **Performance**: Significantly faster with large datasets
7. **Resource Efficiency**: Better thread pool utilization

## API Usage Examples

### Synchronous (Backward Compatible)
```csharp
var graph = ODataNeuronFactory.FromOData(structureIds, 2, endpoint);
ODataSpatialDataFactory.AppendSpatialDataFromOData(graph, endpoint, structureIds, 2);
```

### Async (Recommended)
```csharp
var graph = await ODataNeuronFactory.FromODataAsync(
    structureIds, 2, endpoint, cancellationToken);
await ODataSpatialDataFactory.AppendSpatialDataFromODataAsync(
    graph, endpoint, structureIds, 2, cancellationToken);
```

### Single Node
```csharp
var container = new Container(endpoint);
await ODataSpatialDataFactory.AppendNeuronSpatialDataAsync(
    node, container, cancellationToken);
```

## Testing Strategy

1. **Unit Tests**: Mock Container and test data appending logic
2. **Integration Tests**: Test against real OData service
3. **Performance Tests**: Compare with SimpleODataSpatialDataFactory
4. **Cancellation Tests**: Verify proper cancellation handling
5. **Error Tests**: Verify meaningful error messages

## Migration Notes

- SimpleODataSpatialDataFactory remains for Simple.OData.Client users
- ODataSpatialDataFactory is the new recommended implementation
- Both can coexist in the solution
- API surface is similar for easy migration

