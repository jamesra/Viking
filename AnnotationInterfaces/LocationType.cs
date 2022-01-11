namespace Viking.AnnotationServiceTypes.Interfaces
{ 
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        POLYLINE = 3,
        POLYGON = 4,     //Polygon, no smoothing of exterior verticies with curve fitting
        OPENCURVE = 5,   //Line segments with a line width, additional control points created using curve fitting function
        CURVEPOLYGON = 6, //Polygon whose outer and inner verticies are supplimented with a curve fitting function
        CLOSEDCURVE = 7 //Ring of line segments with a line width
    }; 
}
