namespace WebAnnotation.UI
{
    /// <summary>
    /// </summary>
    public enum RetraceCommandAction
    {
        NONE,
        GROW_EXTERIOR_RING,
        SHRINK_EXTERIOR_RING,
        GROW_INTERNAL_RING,
        SHRINK_INTERNAL_RING,
        CREATE_INTERNAL_RING,  //Create an internal hole in the shape 
        /// <summary>
        /// Replace the exterior ring.  Retain any existing interior rings that are still contained
        /// </summary>
        REPLACE_EXTERIOR_RING,
        /// <summary>
        /// Entirely replace an interior ring, and any intersected existing interior rings 
        /// </summary>
        REPLACE_INTERIOR_RING
    }
}
