using System.Runtime.CompilerServices;
using AnnotationVizLib;

namespace DataExport.Controllers;

/// <summary>
/// Process-wide cache for the volume Scale and the published residual field that every morphology export needs.
/// </summary>
/// <remarks>
/// Registered as a singleton: MVC builds a new <see cref="MorphologyController"/> for every request, so a cache
/// held by the controller would be refetched on each export. Each value is loaded at most once; a load that throws
/// is not cached and the next caller retries it. Thread-safe.
/// </remarks>
public sealed class MorphologyExportCache
{
    private readonly SemaphoreSlim _scaleLock = new(1, 1);
    private readonly SemaphoreSlim _residualFieldLock = new(1, 1);
    private volatile UnitsAndScale.Scale? _scale;

    // Boxed so "loaded, no field available" (a null Value) is published in one write and is distinct from "not loaded".
    private volatile StrongBox<NeighborResidualField?>? _residualField;

    /// <summary>
    /// Returns the cached Scale, calling <paramref name="load"/> only if no earlier call has succeeded.
    /// </summary>
    /// <param name="load">Fetches the Scale, normally from the OData endpoint.</param>
    /// <returns>The Scale shared by every request.</returns>
    public async Task<UnitsAndScale.Scale> GetOrLoadScaleAsync(Func<Task<UnitsAndScale.Scale>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        if (_scale is { } cached)
            return cached;

        await _scaleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return _scale ??= await load().ConfigureAwait(false);
        }
        finally
        {
            _scaleLock.Release();
        }
    }

    /// <summary>
    /// Returns the cached residual field, calling <paramref name="load"/> only if no earlier call has completed.
    /// A load that returns <see langword="null"/> (no field published) is cached too.
    /// </summary>
    /// <param name="load">Loads the field from the correction service or the published directory.</param>
    /// <param name="cancellationToken">Cancels waiting for, or running, the load.</param>
    /// <returns>The residual field shared by every request, or <see langword="null"/> when none is available.</returns>
    public async Task<NeighborResidualField?> GetOrLoadResidualFieldAsync(
        Func<CancellationToken, Task<NeighborResidualField?>> load,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);
        if (_residualField is { } cached)
            return cached.Value;

        await _residualFieldLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _residualField ??= new StrongBox<NeighborResidualField?>(await load(cancellationToken).ConfigureAwait(false));
            return _residualField.Value;
        }
        finally
        {
            _residualFieldLock.Release();
        }
    }
}
