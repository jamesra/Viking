using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace Geometry
{
    /// <summary>
    /// A polygon with interior rings representing holes
    /// Rings are described by points.  The first and last point should match
    /// Uses Counter-Clockwise winding order
    /// </summary>
    [Serializable()]
    public class GridPolygon : IShape2D, ICloneable, IPolygon2D
    {
        double _Area;
        GridVector2[] _ExteriorRing;
         
        public GridVector2[] ExteriorRing
        {
            get { return _ExteriorRing; }
            set
            {
                _Area = value.PolygonArea();
                if (_Area < 0) //Negative area indicates Clockwise orientation, we use counter-clockwise
                {
                    _Area = -_Area;
                    _ExteriorRing = value.Reverse().ToArray();
                }
                else
                {
                    _ExteriorRing = value;
                }

                _Centroid = null;
                _BoundingRect = _ExteriorRing.BoundingBox();
                _ExteriorSegments = CreateLineSegments(_ExteriorRing); 
            }
        }

        GridRectangle _BoundingRect; 
        GridLineSegment[] _ExteriorSegments;

        /// <summary>
        /// Read only please
        /// </summary>
        public GridLineSegment[] ExteriorSegments
        {
            get
            {
                return _ExteriorSegments;
            }
        }

        GridVector2? _Centroid;
        public GridVector2 Centroid
        {
            get
            {
                if(!_Centroid.HasValue)
                {
                    _Centroid = CalculateCentroid(ExteriorRing);
                }

                return _Centroid.Value;
            }
        }

        List<GridPolygon> _InteriorPolygons = new List<GridPolygon>();

        /// <summary>
        /// Read only please
        /// </summary>
        public IReadOnlyCollection<GridPolygon> InteriorPolygons
        {
            get
            {
                return _InteriorPolygons.AsReadOnly();
            }
        }

        /// <summary>
        /// Read only please
        /// </summary>
        public ICollection<GridVector2[]> InteriorRings
        {
            get
            {
                return _InteriorPolygons.Select(p => p._ExteriorRing).ToList();
            }
        }

        public bool HasInteriorRings
        {
            get
            {
                return _InteriorPolygons.Count > 0;
            }
        }

        public GridPolygon(ICollection<IPoint2D> exteriorRing) : this (exteriorRing.Select(p => p.Convert()).ToArray())
        {}


        public GridPolygon(GridVector2[] exteriorRing)
        {
            if(!exteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }

            if(exteriorRing.AreClockwise())
            {
                exteriorRing = exteriorRing.Reverse().ToArray();
            }

            ExteriorRing = exteriorRing;
        }


        public GridPolygon(ICollection<IPoint2D> exteriorRing, ICollection<IPoint2D[]> interiorRings) 
            : this(exteriorRing.Select(p => p.Convert()).ToArray(), 
                   interiorRings.Select(inner_ring => inner_ring.Select(p => p.Convert() ).ToArray()).ToArray())
        { 
        }

        public GridPolygon(GridVector2[] exteriorRing, ICollection<GridVector2[]> interiorRings)
        {
            ExteriorRing = exteriorRing;

            foreach(GridVector2[] interiorRing in interiorRings)
            {
                AddInteriorRing(interiorRing);
            }
        }

        public double Area
        {
            get
            {
                double area = _Area;
                double inner_area = _InteriorPolygons.Sum(ip => ip.Area);
                area -= inner_area;
                return area;
            }
        }

        public double Perimeter
        {
            get
            {
                return ExteriorRing.PerimeterLength();
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return _BoundingRect;
            }
        }

        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.POLYGON;
            }
        }

        ICollection<IPoint2D> IPolygon2D.ExteriorRing
        {
            get
            {
                return this.ExteriorRing.Select(p => p as IPoint2D).ToArray();
            }
        }

        ICollection<IPoint2D[]> IPolygon2D.InteriorRings
        {
            get
            {
                return this.InteriorRings.Select(ir => ir.Select(p => p as IPoint2D).ToArray()).ToArray(); 
            }
        }

        /// <summary>
        /// Total verticies, minus the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalVerticies
        {
            get
            { 
                return ExteriorRing.Length + InteriorRings.Sum(ir => ir.Length);
            }
        }

        /// <summary>
        /// Total verticies, minus the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalUniqueVerticies
        {
            get
            {
                return TotalVerticies - (1 + InteriorRings.Count);
            }
        }

        IPoint2D IPolygon2D.Centroid
        {
            get
            {
                return this.Centroid;
            }
        }

        public void AddInteriorRing(GridVector2[] interiorRing)
        {
            GridPolygon innerPoly = new Geometry.GridPolygon(interiorRing);

            //TODO: Make sure the inner poly does not intersect the outer ring or any existing inner ring
            AddInteriorRing(innerPoly);
        }

        public void AddInteriorRing(GridPolygon innerPoly)
        {
            //TODO: Make sure the inner poly does not intersect the outer ring or any existing inner ring
            
            this._InteriorPolygons.Add(innerPoly);
        }

        /// <summary>
        /// Remove the interior polygon that contains the hole position
        /// </summary>
        /// <param name="holePosition"></param>
        public bool TryRemoveInteriorRing(GridVector2 holePosition)
        {
            for(int iPoly = 0; iPoly < _InteriorPolygons.Count; iPoly++)
            {
                if(_InteriorPolygons[iPoly].Contains(holePosition))
                {
                    _InteriorPolygons.RemoveAt(iPoly);
                    return true;
                }
            }

            return false;
        }

        public void AddVertex(GridVector2 NewControlPointPosition)
        {
            //Find the line segment the NewControlPoint intersects
            double MinDistance;
            int iNearestSegment = this.ExteriorSegments.NearestSegment(NewControlPointPosition, out MinDistance);
            GridLineSegment[] updatedSegments = this.ExteriorSegments.Insert(NewControlPointPosition, iNearestSegment);

            this.ExteriorRing = updatedSegments.Verticies(); 
        }

        public void RemoveVertex(GridVector2 RemovedControlPointPosition)
        {
            double MinDistance;
            GridVector2[] OriginalControlPoints = this.ExteriorRing;
            int iNearestPoint = OriginalControlPoints.NearestPoint(RemovedControlPointPosition, out MinDistance);

            RemoveVertex(iNearestPoint);
        }

        public void RemoveVertex(int iVertex)
        {
            //Find the line segment the NewControlPoint intersects
            GridLineSegment[] updatedLineSegments = ExteriorSegments.Remove(iVertex);

            this.ExteriorRing = updatedLineSegments.Verticies();
        }

        public bool Contains(IPoint2D point_param)
        {
            if (!_BoundingRect.Contains(point_param))
                return false;

            GridVector2 p = new GridVector2(point_param.X, point_param.Y);
            //Create a line we know must pass outside the polygon
            GridLineSegment test_line = new Geometry.GridLineSegment(p, new GridVector2(p.X + (BoundingBox.Width*2), p.Y));

            bool pointInOuterRing = IsInsidePolygon(_ExteriorSegments, test_line);
            if (pointInOuterRing)
            {
                //Check that our point is not inside an interior hole
                foreach(GridPolygon innerPoly in _InteriorPolygons)
                {
                    if(innerPoly.Contains(p))
                    {
                        return false;
                    }
                }
            }

            return pointInOuterRing;
        }

        public bool InteriorPolygonContains(GridVector2 p)
        {
            GridPolygon intersectedPoly;
            return InteriorPolygonContains(p, out intersectedPoly);
        }

        public bool InteriorPolygonContains(GridVector2 p, out GridPolygon interiorPolygon)
        {
            interiorPolygon = null;
            if (!_BoundingRect.Contains(p))
                return false;

            //Check that our point is not inside an interior hole
            foreach (GridPolygon innerPoly in _InteriorPolygons)
            {
                if (innerPoly.Contains(p))
                {
                    interiorPolygon = innerPoly;
                    return true;
                }
            }

            return false;
        }

        

        public GridCircle InscribedCircle()
        {
            GridVector2 center = this.Centroid;
            double Radius = ExteriorRing.Select(p => GridVector2.Distance(center, p)).Min();
            return new GridCircle(center, Radius);
        }

        private static bool IsInsidePolygon(ICollection<GridLineSegment> polygonSegments, GridLineSegment test_line)
        {
            //int numIntersections = polygonSegments.Where(es => es.Intersects(test_line)).Count();

            //In cases where our test line passes exactly through a vertex on the other polygon we double count the line.  
            //This code removes duplicate intersection points to prevent duplicates
            SortedSet<GridVector2> intersectionPoints = new SortedSet<GridVector2>();

            foreach(GridLineSegment line in polygonSegments)
            {
                GridVector2 Intersection;
                if (line.Intersects(test_line, out Intersection))
                {
                    intersectionPoints.Add(Intersection);
                }
            }

            //Inside the polygon if we intersect an odd number of times
            return intersectionPoints.Count % 2 == 1;
        }

        private GridLineSegment[] CreateLineSegments(GridVector2[] ring_points)
        {
            GridLineSegment[] lines = new GridLineSegment[ring_points.Length-1];

            for (int iPoint = 0; iPoint < ring_points.Length-1; iPoint++)
            {
                GridLineSegment line = new Geometry.GridLineSegment(ring_points[iPoint], ring_points[iPoint + 1]);
                lines[iPoint] = line;
            }

            return lines;
        }

        public GridPolygon Rotate(double angle, GridVector2? origin = null)
        {
            if(!origin.HasValue)
            {
                origin = this.Centroid;
            } 
            
            GridVector2[] RotatedRing = this.ExteriorRing.Rotate(angle, origin.Value);

            GridPolygon poly = new GridPolygon(RotatedRing);

            foreach(GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon rotated_inner = innerRing.Rotate(angle, origin);
                poly.AddInteriorRing(rotated_inner);
            }

            return poly;
        }

        public GridPolygon Scale(double scalar, GridVector2? origin = null)
        {
            return this.Scale(new GridVector2(scalar, scalar), origin);
        }

        public GridPolygon Scale(GridVector2 scalar, GridVector2? origin = null)
        {
            if (!origin.HasValue)
            {
                origin = this.Centroid;
            }

            GridVector2[] ScaledRing = this.ExteriorRing.Scale(scalar, origin.Value);

            GridPolygon poly = new GridPolygon(ScaledRing);

            foreach (GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon scaled_inner = innerRing.Scale(scalar, origin);
                poly.AddInteriorRing(scaled_inner);
            }

            return poly;
        }

        public GridPolygon Translate(GridVector2 offset)
        {
            GridVector2[] TranslatedRing = this.ExteriorRing.Translate(offset);

            GridPolygon poly = new GridPolygon(TranslatedRing);

            foreach (GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon translated_inner = innerRing.Translate(offset);
                poly.AddInteriorRing(translated_inner);
            }

            return poly;
        }

        public GridPolygon Smooth(uint NumInterpolationPoints)
        {
            return GridPolygon.Smooth(this, NumInterpolationPoints);
        }

        public static GridVector2 CalculateCentroid(GridVector2[] ExteriorRing, bool ValidateRing = true)
        {
            double accumulator_X = 0;
            double accumulator_Y = 0;

            //To prevent precision errors we subtract the average value and add it again
            ExteriorRing = ExteriorRing.EnsureClosedRing().ToArray();
            GridVector2 Average = ExteriorRing.Average();
            GridVector2[] translated_Points = ExteriorRing.Translate(-Average);

            for (int i = 0; i < translated_Points.Length - 1; i++)
            {
                GridVector2 p0 = translated_Points[i];
                GridVector2 p1 = translated_Points[i + 1];
                double SharedTerm = ((p0.X * p1.Y) - (p1.X * p0.Y));
                accumulator_X += (p0.X + p1.X) * SharedTerm;
                accumulator_Y += (p0.Y + p1.Y) * SharedTerm;
            }

            double ExteriorArea = translated_Points.PolygonArea();
            double scalar = ExteriorArea * 6;

            return new GridVector2((accumulator_X / scalar) + Average.X, (accumulator_Y / scalar) + Average.Y);
        }

        public static GridPolygon Smooth(GridPolygon poly, uint NumInterpolationPoints)
        {
            GridVector2[] smoothedCurve = poly.ExteriorRing.CalculateCurvePoints(NumInterpolationPoints, true);

            GridPolygon smoothed_poly = new GridPolygon(smoothedCurve);

            foreach(GridPolygon inner_poly in poly.InteriorPolygons)
            {
                GridPolygon smoother_inner_poly = GridPolygon.Smooth(inner_poly, NumInterpolationPoints);
                smoothed_poly.AddInteriorRing(smoother_inner_poly);
            }

            return smoothed_poly;
        }

        public double Distance(GridVector2 p)
        {
            return this.ExteriorSegments.Min(line => line.DistanceToPoint(p));
        }

        public double Distance(GridVector2 p, out GridLineSegment nearestLine)
        {
            double minDistance = double.MaxValue;
            nearestLine = ExteriorSegments.First();

            for (int i = 0; i < ExteriorSegments.Length; i++)
            {
                double dist = ExteriorSegments[i].DistanceToPoint(p);
                if(dist < minDistance)
                {
                    nearestLine = ExteriorSegments[i];
                    minDistance = dist;
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Brute force search for distance
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Distance(GridLineSegment p)
        {
            double minDistanceA = Distance(p.A);
            double minDistanceB = Distance(p.B);
            double minDistanceLine = ExteriorRing.Min(es => p.DistanceToPoint(es));

            return new double[] { minDistanceA, minDistanceB, minDistanceLine }.Min();
        }

        /// <summary>
        /// Brute force search for distance
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Distance(GridPolygon other)
        {
            if (this.Intersects(other))
                return 0; 

            double minDistanceToOtherLineSegment = this.ExteriorRing.Min(p => other.Distance(p));
            double minDistanceToThisLineSegment = other.ExteriorRing.Min(p => this.Distance(p));

            return Math.Min(minDistanceToOtherLineSegment, minDistanceToThisLineSegment);
        }

        /// <summary>
        /// Given a point inside the polygon return the normalized distance.
        /// Create a line passing through the centroid and the point. 
        /// Locate the nearest intersecting line segment in the exterior ring.
        /// Measure the distance
        /// </summary>
        /// <param name="p"></param>
        public double DistanceFromCenterNormalized(GridVector2 p)
        {
            GridLine line = new Geometry.GridLine(Centroid, p - Centroid);

            List<GridVector2> Intersections = new List<Geometry.GridVector2>(ExteriorRing.Length);
            for (int i = 0; i < _ExteriorSegments.Length; i++)
            {
                GridVector2 intersection;
                if (line.Intersects(this._ExteriorSegments[i], out intersection))
                {
                    double CenterDist = GridVector2.Distance(Centroid, intersection);
                    double PointDist = GridVector2.Distance(p, intersection);
                    //Since the line is infinite we need to ignore cases where the intersection is between the point and the line, or on the other side of the center point
                    //  I------P---------I-----I------C-------I
                    //  In the example above slicing through a concave poly we'd want the first Intersection (I) to determine the distance from center normalized
                    
                }
            }

            throw new NotImplementedException();
        }

        public int[] VerticiesOnConvexHull()
        {
            int[] indicies; 
            GridVector2[] convex_hull_verts = this.ExteriorRing.ConvexHull(out indicies);

            return indicies;
        }

        public object Clone()
        {
            GridPolygon clone = new Geometry.GridPolygon(this.ExteriorRing.Clone() as GridVector2[]);
            foreach(GridPolygon innerPoly in this.InteriorPolygons)
            {
                GridPolygon innerClone = innerPoly.Clone() as GridPolygon;
                clone.AddInteriorRing(innerClone);
            }

            return clone;
        }
          
        public bool Intersects(IShape2D shape)
        {
            return ShapeExtensions.PolygonIntersects(this, shape);
        }


        public bool Intersects(ICircle2D c)
        {
            GridCircle circle = c.Convert();
            return this.Intersects(circle);
        }

        public bool Intersects(GridCircle circle)
        {
            return PolygonIntersectionExtensions.Intersects(this, circle);
        }


        public bool Intersects(ILineSegment2D l)
        {
            GridLineSegment line = l.Convert();
            return this.Intersects(line);
        }

        public bool Intersects(GridLineSegment line)
        {
            return PolygonIntersectionExtensions.Intersects(this, line);
        }

        public bool Intersects(ITriangle2D t)
        {
            GridTriangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(GridTriangle tri)
        {
            return PolygonIntersectionExtensions.Intersects(this, tri);
        }

        public bool Intersects(IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(poly);
        }

        public bool Intersects(GridPolygon poly)
        {
            return PolygonIntersectionExtensions.Intersects(this, poly);
        }

        bool IShape2D.Contains(IPoint2D p)
        {
            return this.Contains(p.Convert());
        }
         
        IShape2D IShape2D.Translate(IPoint2D offset)
        {
            GridVector2 v = offset.Convert();
            return this.Translate(v);
        }

        
    }
}
