# Async ODataMorphologyFactory Optimization

## Current Issues

The `ODataMorphologyFactory` has several critical problems:

### Fake Async Methods (Lines 45-167)
- `FromODataByTypeIDsAsync` (line 45): Wraps synchronous code in `Task.Run()` - not truly async
- `FromODataAsync` (line 77): Wraps synchronous code in `Task.Run()` - not truly async  
- `FromODataLocationIDsAsync` (line 112): Wraps synchronous code in `Task.Run()` - not truly async
- These provide no real async benefits and waste thread pool resources

### Blocking Synchronous Operations
- Line 17: `container.Scale().GetValue()` - blocking call to retrieve scale
- Lines 29, 100, 130, 145: `.FirstOrDefault()` - blocking LINQ materializations
- Lines 34, 152: `.Load()` - blocking loads of location links
- Line 68: `.ToList()` - blocking list materializations in loops
- Lines 201, 221, 226: `.ToList()` - blocking queries in recursive methods

### Sequential Loading Anti-Patterns
- Lines 27-37: Loading structures **one-by-one** in a loop instead of batch query
- Lines 61-70: Loading structures **one-by-one** in a loop (FromODataByTypeIDsAsync)
- Lines 92-105: Loading structures **one-by-one** in a loop (FromODataAsync)
- Lines 128-135: Loading locations **one-by-one** in a loop
- Lines 171-175: Loading structure location links **one-by-one** in a loop

### Missing Features
- No `CancellationToken` support in any async method
- No proper error handling or retry logic
- No batch optimization for OData queries
- Synchronous scale retrieval repeated in every method

### Inefficient Patterns
- Line 188: `Parallel.ForEach` used but not truly parallel with OData calls
- Lines 234-244: `LoadStructureLocationLinksAsync` wraps sync code in `Task.Run()`
- Line 159: `ContainsKey` check without using `TryGetValue`
- Recursive structure loading (line 201-203) is synchronous and inefficient

## Optimizations from SimpleODataMorphologyFactory

Key patterns to adopt:

1. **Batch Task Creation**: Create all tasks first, then `await Task.WhenAll(tasks)`
2. **True Async Methods**: Use proper async/await with OData query extensions
3. **Parallel Data Fetching**: Fetch multiple structures in parallel tasks
4. **Async Scale Retrieval**: Make scale fetching async and cache it
5. **Efficient Location Link Loading**: Use `ExecuteFunctionAsArrayAsync` with continuations
6. **Better Structure Loading**: Single async method that handles all structure loading patterns
7. **Task Composition**: Use `ContinueWith` for dependent operations

## Implementation Plan

### 1. Create Async Scale Retrieval Helper

**New Method: `GetScaleAsync`**
```csharp
private static async Task<UnitsAndScale.Scale> GetScaleAsync(
    Container container, 
    CancellationToken cancellationToken = default)
{
    // Use the OData Scale function properly with async
    var scaleTask = Task.Run(() => container.Scale().GetValue(), cancellationToken);
    var scale = await scaleTask;
    return scale.ToGeometryScale();
}
```

### 2. Implement Batch Structure Loading

**New Method: `LoadStructuresByIDsAsync`**
```csharp
private static async Task<List<Structure>> LoadStructuresByIDsAsync(
    Container container,
    ICollection<long> structureIDs,
    CancellationToken cancellationToken = default)
{
    // Create batch tasks for parallel loading
    var tasks = structureIDs.Select(id =>
        container.Structures
            .Expand(s => s.Locations)
            .Expand(s => s.Type)
            .Expand(s => s.Children)
            .Where(s => s.ID == id)
            .GetAllPagesToListAsync(cancellationToken)
    ).ToList();
    
    await Task.WhenAll(tasks);
    return tasks.SelectMany(t => t.Result).ToList();
}
```

### 3. Implement Batch Structure Loading by Type

**New Method: `LoadStructuresByTypeIDsAsync`**
```csharp
private static async Task<List<Structure>> LoadStructuresByTypeIDsAsync(
    Container container,
    ICollection<long> typeIDs,
    CancellationToken cancellationToken = default)
{
    // Similar to above but filter by TypeID
    var tasks = typeIDs.Select(typeId =>
        container.Structures
            .Expand(s => s.Locations)
            .Expand(s => s.Type)
            .Expand(s => s.Children)
            .Where(s => s.TypeID == typeId)
            .GetAllPagesToListAsync(cancellationToken)
    ).ToList();
    
    await Task.WhenAll(tasks);
    return tasks.SelectMany(t => t.Result).ToList();
}
```

### 4. Implement Async Location Link Loading

**Replace `LoadStructureLocationLinks` (line 169-176) with:**
```csharp
private static async Task LoadStructureLocationLinksAsync(
    Container container,
    ICollection<Structure> structures,
    CancellationToken cancellationToken = default)
{
    var tasks = structures
        .Where(s => s.LocationLinks == null || !s.LocationLinks.Any())
        .Select(async s =>
        {
            var links = await container.StructureLocationLinks(s.ID)
                .GetAllPagesToListAsync(cancellationToken);
            s.LocationLinks = new Microsoft.OData.Client.DataServiceCollection<LocationLink>();
            foreach (var link in links)
            {
                s.LocationLinks.Add(link);
            }
        });
    
    await Task.WhenAll(tasks);
}
```

### 5. Implement Async Location Loading

**New Method: `LoadLocationsByIDsAsync`**
```csharp
private static async Task<List<Location>> LoadLocationsByIDsAsync(
    Container container,
    ICollection<long> locationIDs,
    UnitsAndScale.Scale scale,
    CancellationToken cancellationToken = default)
{
    var tasks = locationIDs.Distinct().Select(id =>
        container.Locations
            .Where(l => l.ID == id)
            .GetAllPagesToListAsync(cancellationToken)
    ).ToList();
    
    await Task.WhenAll(tasks);
    var locations = tasks.SelectMany(t => t.Result).ToList();
    
    // Set scale on all locations
    foreach (var loc in locations)
    {
        // Assuming Location has a scale property
    }
    
    return locations;
}
```

### 6. Convert FromOData to True Async

**Update `FromOData` (line 11):**
```csharp
public static NeuronGraph FromOData(
    ICollection<long> StructureIDs, 
    bool include_children, 
    Uri Endpoint)
{
    return FromODataAsync(StructureIDs, include_children, Endpoint)
        .GetAwaiter().GetResult();
}
```

### 7. Rewrite FromODataAsync (line 77)

Remove `Task.Run` wrapper and make truly async:
```csharp
public static async Task<MorphologyGraph> FromODataAsync(
    ICollection<long> StructureIDs, 
    bool include_children, 
    Uri Endpoint,
    CancellationToken cancellationToken = default)
{
    Container container = new Container(Endpoint)
    {
        MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
    };
    
    var scale = await GetScaleAsync(container, cancellationToken);
    MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
    
    if (StructureIDs == null || StructureIDs.Count == 0)
        return rootGraph;
    
    // Load structures in parallel
    var structures = await LoadStructuresByIDsAsync(
        container, StructureIDs, cancellationToken);
    
    // Load location links in parallel
    await LoadStructureLocationLinksAsync(
        container, structures, cancellationToken);
    
    // Build morphology graphs
    await MorphologyForStructuresAsync(
        container, rootGraph, structures, include_children, scale, cancellationToken);
    
    return rootGraph;
}
```

### 8. Rewrite FromODataByTypeIDsAsync (line 45)

Similar pattern - remove `Task.Run` wrapper:
```csharp
public static async Task<MorphologyGraph> FromODataByTypeIDsAsync(
    ICollection<long> TypeIDs, 
    Uri Endpoint, 
    bool include_children = false,
    CancellationToken cancellationToken = default)
{
    Container container = new Container(Endpoint)
    {
        MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
    };
    
    var scale = await GetScaleAsync(container, cancellationToken);
    MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
    
    if (TypeIDs == null || TypeIDs.Count == 0)
        return rootGraph;
    
    // Load structures by type IDs in parallel
    var structures = await LoadStructuresByTypeIDsAsync(
        container, TypeIDs, cancellationToken);
    
    await LoadStructureLocationLinksAsync(
        container, structures, cancellationToken);
    
    await MorphologyForStructuresAsync(
        container, rootGraph, structures, include_children, scale, cancellationToken);
    
    return rootGraph;
}
```

### 9. Rewrite FromODataLocationIDsAsync (line 112)

Remove `Task.Run` wrapper and implement proper async:
```csharp
public static async Task<MorphologyGraph> FromODataLocationIDsAsync(
    ICollection<long> LocationIDs, 
    Uri Endpoint, 
    int hops = 0,
    CancellationToken cancellationToken = default)
{
    Container container = new Container(Endpoint)
    {
        MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
    };
    
    var scale = await GetScaleAsync(container, cancellationToken);
    MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
    
    if (LocationIDs == null || LocationIDs.Count == 0)
        return rootGraph;
    
    // Load initial locations in parallel
    var locations = await LoadLocationsByIDsAsync(
        container, LocationIDs, scale, cancellationToken);
    
    if (locations.Count == 0)
        return rootGraph;
    
    // Get parent structure
    long structureId = locations[0].ParentID;
    var parentTask = LoadStructuresByIDsAsync(
        container, new[] { structureId }, cancellationToken);
    
    await parentTask;
    var parent = parentTask.Result.FirstOrDefault();
    
    if (parent == null)
        return rootGraph;
    
    // Load location links
    await LoadStructureLocationLinksAsync(
        container, new[] { parent }, cancellationToken);
    
    // Build graph
    MorphologyGraph graph = MorphologyForStructure(parent, scale);
    
    // Add requested locations to graph
    foreach (var loc in locations)
    {
        if (!graph.Nodes.ContainsKey((ulong)loc.ID))
        {
            graph.AddNode(new MorphologyNode(
                (ulong)loc.ID, 
                new ODataLocationAdapter(loc, scale), 
                graph));
        }
    }
    
    AddLocationEdges(graph, parent.LocationLinks.ToArray());
    
    // TODO: Implement hops logic with async/await
    
    return graph;
}
```

### 10. Convert MorphologyForStructures to Fully Async

**Replace current implementation (line 183-207):**
```csharp
private static async Task MorphologyForStructuresAsync(
    Container container,
    MorphologyGraph rootGraph,
    ICollection<Structure> structures,
    bool include_children,
    UnitsAndScale.Scale scale,
    CancellationToken cancellationToken = default)
{
    foreach (Structure s in structures)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        MorphologyGraph graph = MorphologyForStructure(s, scale);
        if (graph == null)
            continue;
        
        rootGraph.AddSubgraph(graph);
        
        if (include_children && s.Children != null && s.Children.Any())
        {
            // Load child structures
            var childIds = s.Children.Select(c => (long)c.ID).ToList();
            var childStructures = await LoadStructuresByIDsAsync(
                container, childIds, cancellationToken);
            
            await LoadStructureLocationLinksAsync(
                container, childStructures, cancellationToken);
            
            // Recursively process children
            await MorphologyForStructuresAsync(
                container, graph, childStructures, 
                include_children, scale, cancellationToken);
        }
    }
}
```

### 11. Remove Dead/Duplicate Async Methods

- **Line 209-232**: Remove `MorphologyForStructuresAsync` (old implementation)
- **Line 234-244**: Remove `LoadStructureLocationLinksAsync` (old implementation wrapping sync code)

These will be replaced by the new implementations above.

### 12. Optimize Dictionary Operations

**Line 159**: Replace `ContainsKey` check:
```csharp
// Current:
if (!graph.Nodes.ContainsKey((ulong)loc.ID))

// Better (if adding to dictionary):
if (!graph.Nodes.TryGetValue((ulong)loc.ID, out var existingNode))
```

### 13. Add Error Handling

Wrap all async operations in try-catch blocks:
```csharp
try
{
    var structures = await LoadStructuresByIDsAsync(...);
}
catch (Exception ex) when (!(ex is OperationCanceledException))
{
    throw new InvalidOperationException(
        $"Failed to load structures: {ex.Message}", ex);
}
```

### 14. Add CancellationToken Checks in Loops

In all loops over collections:
```csharp
foreach (var item in collection)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ... process item
}
```

## Key Files to Modify

- `ODataMorphologyFactory.cs` - Main refactoring target

## Performance Benefits

After implementation:

1. **True Async**: No thread pool blocking with `Task.Run` wrappers
2. **Parallel Loading**: Multiple structures/locations loaded simultaneously
3. **Batch Optimization**: Fewer round trips to OData service
4. **Cancellation Support**: Can cancel long-running operations
5. **Better Resource Usage**: Async I/O doesn't block threads
6. **Faster Response**: Parallel fetching significantly reduces total time

## Testing Considerations

Verify after implementation:

1. Backward compatibility with synchronous `FromOData` method
2. All async methods are truly async (no `Task.Run` wrappers)
3. Parallel loading actually improves performance
4. Cancellation tokens work correctly
5. Error handling provides useful messages
6. Memory usage is reasonable with large datasets
7. Recursive child loading works correctly
8. Location link loading completes before graph building

## Migration Notes

- The synchronous `FromOData` method should remain for backward compatibility
- Callers using the async methods may see significant performance improvements
- Consider adding batch size limits for very large structure/location sets
- May want to add progress reporting for long-running operations

