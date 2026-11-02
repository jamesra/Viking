namespace WebAnnotation.WPF.Forms
{
    /// <summary>
    /// Look of the one-shot shimmer played over a newly shown auto-polygonize proposal. Lives in
    /// WebAnnotationWPFControls so the preferences dialog and WebAnnotation (which references this project) share it.
    /// The integer values are what the user setting stores, so do not renumber them.
    /// </summary>
    public enum AutoPolygonizeShimmerStyle
    {
        /// <summary>Soft white glow fading in and out with a soft band sweeping across at a random angle.</summary>
        Sweep = 0,

        /// <summary>A drop of water landing at the centre: three circular rings spread out to the edges.</summary>
        Ripple = 1,

        /// <summary>Each shimmer picks <see cref="Sweep"/> or <see cref="Ripple"/> at random when it starts.</summary>
        Random = 2
    }
}
