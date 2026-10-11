namespace ConnectomeODataV4.Controllers
{
    /// <summary>
    /// Limits the <c>Hops</c> argument of the OData network functions (<c>Network</c>,
    /// <c>NetworkChildStructures</c>, <c>NetworkLinks</c>, <c>NetworkSpatialData</c>,
    /// <c>NetworkEdgeSpatialData</c>) before it reaches SQL. Called by the controllers that
    /// expose those routes.
    /// </summary>
    public static class NetworkHops
    {
        /// <summary>
        /// Largest hop count the OData layer passes to the network SQL functions.
        /// <c>NetworkStructureIDs</c> runs one loop iteration per hop with several joins each, so a
        /// caller-supplied value near <see cref="int.MaxValue"/> is a cost risk; the loop already
        /// stops once a hop finds no new cells, and real networks reach that fixpoint long before
        /// 1024 hops, so clamping to this value does not change results on real data.
        /// </summary>
        public const int MaxHops = 1024;

        /// <summary>
        /// Returns <paramref name="hops"/> clamped to at most <see cref="MaxHops"/>. Values above the
        /// limit are clamped, not rejected; values at or below zero pass through unchanged so SQL
        /// keeps its "seed IDs only" behavior for them.
        /// </summary>
        public static int Clamp(int hops) => hops > MaxHops ? MaxHops : hops;
    }
}
