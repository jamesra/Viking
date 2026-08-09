namespace Viking.AnnotationServiceTypes.Interfaces
{ 
    public enum LocationType
    {
        POINT = 0,
        /// <summary>
        /// A circle, note this will appear as a CURVEPOLYGON to SQL because of how OpenGIS encodes the circle
        /// </summary>
        CIRCLE = 1,
        /// <summary>
        /// Currently unused, but easy to describe mathematically
        /// </summary>
        ELLIPSE = 2,
        /// <summary>
        /// Vertices describing a poly-line with no smoothing
        /// </summary>
        POLYLINE = 3,
        /// <summary>
        /// Polygon, no smoothing of exterior vertices with curve fitting
        /// </summary>
        POLYGON = 4,     
        /// <summary>
        /// Line segments with a line width, control points supplemented using curve fitting function
        /// </summary>
        OPENCURVE = 5,   
        /// <summary>
        /// Polygon whose outer and inner vertices are supplemented and smoothed with a curve fitting function, not be confused with a CIRCLE, which is encoded in OpenGIS as a CURVEPOLYGON in SQL.
        /// </summary>
        CURVEPOLYGON = 6, 
        /// <summary>
        /// Ring of line segments with a line width, supplemented with a curve fitting function, the interior is not part of the shape
        /// </summary>
        CLOSEDCURVE = 7
    }; 
}
