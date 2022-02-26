using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    /// <summary>
    /// We have a large number of grid transforms which have the same dimensions, rather than calculate a new mesh for each transform we just 
    /// Build it once and refer to the existing mesh for a given grid size
    /// </summary>
    static class GridTransformHelper
    {
        static readonly ConcurrentDictionary<GridVector2, int[]> TriangleIndexDictionary = new ConcurrentDictionary<GridVector2, int[]>();
        //        static ConcurrentDictionary<GridVector2, MappingGridTriangle[]> TriangleListDictionary = new ConcurrentDictionary<GridVector2, MappingGridTriangle[]>();

        static readonly ConcurrentDictionary<GridVector2, List<int>[]> EdgesDictionary = new ConcurrentDictionary<GridVector2, List<int>[]>();

        public static int IndexForCoord(int x, int y, int GridSizeX, int GridSizeY)
        {
            return y + (x * GridSizeY);
        }

        /// <summary>
        /// Returns triangles for a grid of specified size, thread safe
        /// </summary>
        /// <param name="GridSizeX"></param>
        /// <param name="GridSizeY"></param>
        /// <returns></returns>
        public static int[] TrianglesForGrid(int GridSizeX, int GridSizeY)
        {
            GridVector2 key = new GridVector2(GridSizeX, GridSizeY);


            bool success = TriangleIndexDictionary.TryGetValue(key, out int[] Indicies);
            if (!success)
            {
                Indicies = new int[(GridSizeX - 1) * (GridSizeY - 1) * 6];
                int iNextIndex = 0;


                for (int x = 0; x < GridSizeX - 1; x++)
                {
                    for (int y = 0; y < GridSizeY - 1; y++)
                    {
                        /*
                        int botLeft = x + (y * GridSizeX);
                        int botRight = (x + 1) + (y * GridSizeX);
                        int topLeft = x + ((y + 1) * GridSizeX);
                        int topRight = (x + 1) + ((y + 1) * GridSizeX);
                        */

                        int botLeft = y + (x * GridSizeY);
                        int topLeft = (y + 1) + (x * GridSizeY);
                        int botRight = y + ((x + 1) * GridSizeY);
                        int topRight = (y + 1) + ((x + 1) * GridSizeY);


                        //int[] triangles = new int[] { botLeft, botRight, topLeft, botRight, topRight, topLeft };
                        //triangleIndicies.AddRange(triangles);
                        int[] newIndicies = new int[] { botLeft, botRight, topLeft, botRight, topRight, topLeft };
                        newIndicies.CopyTo(Indicies, iNextIndex);
                        iNextIndex += newIndicies.Length;
                    }
                }

                //Get the value in the dictionary if it exists so we don't keep two copies hanging around
                Indicies = TriangleIndexDictionary.GetOrAdd(key, Indicies);
            }

            return Indicies;
        }

        public static MappingGridTriangle TriangleForPoint(int GridSizeX, int GridSizeY, in GridRectangle Bounds, MappingGridVector2[] points, int[] TriIndicies, GridVector2 Point)
        {
            //Having a smaller epsilon caused false positives.  
            //We just want to know if we are close enough to check with the more time consuming math
            double epsilon = 5;

            Point = Point.Round(Global.TransformSignificantDigits);

            if (!Bounds.Contains(Point, epsilon))
                return null;

            double OffsetX = Point.X - Bounds.Left;
            double OffsetY = Point.Y - Bounds.Bottom;

            double X = (OffsetX / Bounds.Width) * (GridSizeX - 1);
            double Y = (OffsetY / Bounds.Height) * (GridSizeY - 1);

            int iX = (int)X;
            int iY = (int)Y;

            //This gives us the grid coordinates which contains two triangles, however there are two triangles.  If the fractional parts add up to a number greater than one it is the upper triangle.
            bool IsUpper = (X - iX) + (Y - iY) > 1;

            //Check edge case where point is exactly on the right edge of the boundary
            if (OffsetX + double.Epsilon >= Bounds.Width)
            {
                IsUpper = true;
                iX--;
            }
            else if (OffsetY + double.Epsilon >= Bounds.Height)
            {
                IsUpper = true;
                iY--;
            }
            else
            {
                IsUpper = (X - iX) + (Y - iY) > 1;
            }

            int iTri = (iY << 1) + (((GridSizeY - 1) << 1) * iX); //(iY * 2) + ((GridSizeY - 1) * 2 * iX)
            //int iTri = (iX * 2) + ((GridSizeX-1) * 2 * iY);
            iTri += IsUpper ? 1 : 0;
            iTri *= 3;//Multiply by three to get the triangle offset

            MappingGridTriangle mapTri = new MappingGridTriangle(points, TriIndicies[iTri], TriIndicies[iTri + 1], TriIndicies[iTri + 2]);

            Debug.Assert(mapTri.CanTransform(Point.Round(Global.TransformSignificantDigits)), "Calculated GridTransform does not intersect requested point");
            return mapTri;
        }

        /// <summary>
        /// Returns edges for a grid of set size, thread safe
        /// </summary>
        /// <param name="GridSizeX"></param>
        /// <param name="GridSizeY"></param>
        /// <returns></returns>
        public static List<int>[] EdgesForGrid(int GridSizeX, int GridSizeY)
        {
            GridVector2 key = new GridVector2(GridSizeX, GridSizeY);


            bool success = EdgesDictionary.TryGetValue(key, out List<int>[] edges);
            if (!success)
            {
                edges = new List<int>[GridSizeX * GridSizeY];

                //Prepopulate edges so we don't have to constantly test for existence in the loop
                for (int i = 0; i < edges.Length; i++)
                {
                    edges[i] = new List<int>(6); //The max number of edges for a grid point
                }

                for (int x = 0; x < GridSizeX; x++)
                {
                    for (int y = 0; y < GridSizeY; y++)
                    {
                        int iPoint = y + (x * GridSizeY); //The edges we are populating

                        if (y + 1 < GridSizeY)
                        {
                            int iAbove = (y + 1) + (x * GridSizeY);
                            edges[iPoint].Add(iAbove);
                            edges[iAbove].Add(iPoint);

                            if (x - 1 > 0)
                            {
                                int iAboveLeft = (y + 1) + ((x - 1) * GridSizeY);
                                edges[iPoint].Add(iAboveLeft);
                                edges[iAboveLeft].Add(iPoint);
                            }
                        }

                        if (x + 1 < GridSizeX)
                        {
                            int iRight = y + ((x + 1) * GridSizeY);
                            edges[iPoint].Add(iRight);
                            edges[iRight].Add(iPoint);
                        }
                    }
                }

                //Get the value in the dictionary if it exists so we don't keep two copies hanging around
                edges = EdgesDictionary.GetOrAdd(key, edges);
            }

            return edges;
        }
    }

    /// <summary>
    /// A grid transform is a uniform grid of dimensions X,Y with the points equally spaced throughout the grid
    /// </summary>
    [Serializable()]
    public class GridTransform : TriangulationTransform, IGridTransformInfo
    {
        /// <summary>
        /// Size of x dimension of grid 
        /// </summary>
        public int GridSizeX { get; protected set; }

        /// <summary>
        /// Size of y dimension of grid 
        /// </summary>
        public int GridSizeY { get; protected set; }

        public override int[] TriangleIndicies
        {
            get
            {
                if (_TriangleIndicies == null)
                {
                    _TriangleIndicies = GridTransformHelper.TrianglesForGrid(GridSizeX, GridSizeY);
                }

                return _TriangleIndicies;
            }
        }

        private List<int>[] _Edges = null;
        public override List<int>[] Edges
        {
            get
            {
                if (_Edges == null)
                    _Edges = GridTransformHelper.EdgesForGrid(GridSizeX, GridSizeY);

                return _Edges;
            }
            protected set
            {
                _Edges = value;
            }
        }

        public GridTransform(MappingGridVector2[] points, GridRectangle mappedBounds, int gridSizeX, int gridSizeY, TransformBasicInfo info)
            : base(points, mappedBounds, info)
        {
            GridSizeX = gridSizeX;
            GridSizeY = gridSizeY;

            Array.Sort(points);

            Debug.Assert(points.Length == gridSizeX * gridSizeY, "Invalid argument to GridTransform constructor.  Number of points incorrect");
            if (points.Length != gridSizeX * gridSizeY)
            {
                throw new ArgumentException("GridTransform constructor. Number of points incorrect");
            }

            _TriangleIndicies = GridTransformHelper.TrianglesForGrid(GridSizeX, GridSizeY);
        }

        protected GridTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            GridSizeX = (int)info.GetValue("GridSizeX", typeof(int));
            GridSizeY = (int)info.GetValue("GridSizeY", typeof(int));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue("GridSizeX", GridSizeX);
            info.AddValue("GridSizeY", GridSizeY);

            base.GetObjectData(info, context);
        }

        /// <summary>
        /// Returns the coordinate on the section to be mapped given a grid coordinate from reading the transform
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static GridVector2 CoordinateFromGridPos(int x, int y, double gridWidth, double gridHeight, double MappedWidth, double MappedHeight)
        {
            return new GridVector2(((x) / (gridWidth - 1)) * MappedWidth, (y / (gridHeight - 1)) * MappedHeight);
        }

        /// <summary>
        /// Returns the coordinate on the section to be mapped given a grid coordinate from reading the transform
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public GridVector2 CoordinateFromGridPos(int x, int y, double gridWidth, double gridHeight)
        {
            return new GridVector2(((x) / (gridWidth - 1)) * (double)MappedBounds.Width, (y / (gridHeight - 1)) * (double)MappedBounds.Height);
        }

        /// <summary>
        /// Return the control triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal override MappingGridTriangle GetTransform(in GridVector2 Point)
        {
            //Having a smaller epsilon caused false positives.  
            //We just want to know if we are close enough to check with the more time consuming math
            double epsilon = 0;

            if (!MappedBounds.Contains(Point, epsilon))
                return null;

            //Triangles are ordered from left to right, and then bottom to top
            return GridTransformHelper.TriangleForPoint(this.GridSizeX, GridSizeY, MappedBounds, this.MapPoints, this.TriangleIndicies, Point);
        }

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        internal override MappingGridTriangle GetInverseTransform(in GridVector2 Point)
        {
            //Fetch a list of triangles from the nearest point
            List<MappingGridTriangle> triangles = controlTrianglesRTree.Intersects(Point.ToRTreeRect(0));

            if (triangles == null)
                return null;

            foreach (MappingGridTriangle t in triangles)
            {
                if (!t.ControlBoundingBox.Contains(Point))
                    continue;

                if (t.CanInverseTransform(Point))
                    return t;
            }

            return null;
        }

        [FlagsAttribute]
        private enum Direction
        {
            NONE = 0x0,
            LEFT = 0x1,
            TOP = 0x2,
            RIGHT = 0x4,
            BOTTOM = 0x8,
            TOPLEFT = 0x3,
            TOPRIGHT = 0x6,
            BOTTOMLEFT = 0x9,
            BOTTOMRIGHT = 0xC,
            ALL = 0xF
        };

        public override double ConvexHullIntersection(GridLineSegment L, GridVector2 OutsidePoint, out GridLineSegment foundCtrlLine, out GridLineSegment foundMapLine, out GridVector2 intersection)
        {
            double distance = double.MaxValue;
            foundCtrlLine = new GridLineSegment();
            foundMapLine = new GridLineSegment();
            intersection = new GridVector2();
            //In the grid transform we can simply calculate where the edge intersects if needed
            //The only place we expect this to be called is for intersections with the outside border, but we should implement it completely to be safe

            //Check the edges first
            if (MappedBounds.Intersects(L.BoundingBox) == false)
                return distance;

            GridLineSegment[] Borders = new GridLineSegment[] { MappedBounds.LeftEdge,
                                                                MappedBounds.RightEdge,
                                                                MappedBounds.TopEdge,
                                                                MappedBounds.BottomEdge};

            Direction[] BorderDir = new Direction[] { Direction.LEFT,
                                                      Direction.RIGHT,
                                                      Direction.TOP,
                                                      Direction.BOTTOM};

            GridVector2 BestIntersection = new GridVector2();
            Direction IntersectDir = Direction.NONE;
            for (int iBorder = 0; iBorder < Borders.Length; iBorder++)
            {
                bool success = L.Intersects(Borders[iBorder], out GridVector2 BorderIntersect);
                if (success)
                {
                    double IntersectDistance = GridVector2.Distance(OutsidePoint, BorderIntersect);
                    if (IntersectDistance < distance)
                    {
                        distance = IntersectDistance;
                        BestIntersection = BorderIntersect;
                        IntersectDir = BorderDir[iBorder];
                        intersection = BestIntersection;
                    }
                }
            }

            if (IntersectDir == Direction.NONE)
            {
                //Nothing found, stop
                return distance;
            }

            //Figure out the coordinates for L.A
            double X = ((BestIntersection.X - MappedBounds.Left) / MappedBounds.Width) * (GridSizeX - 1);
            double Y = ((BestIntersection.Y - MappedBounds.Bottom) / MappedBounds.Height) * (GridSizeY - 1);

            int iX = (int)X;
            int iY = (int)Y;


            GridLineSegmentPair pair = LinesForCoord(iX, iY, IntersectDir);

            GridVector2 testIntersection;


            double RoundErrorTestValue = 0;

            if (IntersectDir == Direction.RIGHT || IntersectDir == Direction.LEFT)
            {
                RoundErrorTestValue = X - Math.Floor(X);
            }
            else
            {
                RoundErrorTestValue = Y - Math.Floor(Y);
            }

            if (RoundErrorTestValue > 0.99)
            {
                //OK, better check if there is a rounding error we need to correct.
                if (!L.Intersects(foundMapLine, out testIntersection))
                {
                    //OK, probably a rounding error for a point very close to the end of the line
                    if (IntersectDir == Direction.RIGHT || IntersectDir == Direction.LEFT)
                    {
                        iX = (int)Math.Round(X);
                    }
                    else
                    {
                        iY = (int)Math.Round(Y);
                    }

                    pair = LinesForCoord(iX, iY, IntersectDir);
                    Debug.Assert(L.Intersects(pair.mapLine, out testIntersection));
                }



            }

            Debug.Assert(L.Intersects(pair.mapLine, out testIntersection));

            foundCtrlLine = pair.ctrlLine;
            foundMapLine = pair.mapLine;

            return distance;
        }

        /// <summary>
        /// Returns the mapping line and control line for a set of
        /// </summary>
        /// <param name="iX"></param>
        /// <param name="iY"></param>
        /// <param name="Dir"></param>
        /// <returns></returns>
        private GridLineSegmentPair LinesForCoord(int iX, int iY, Direction IntersectDir)
        {
            //Find the nearest line segment
            int iStart = GridTransformHelper.IndexForCoord(iX, iY, GridSizeX, GridSizeY);
            int iEnd;

            //Bottom edge intersection
            if ((IntersectDir & (Direction.LEFT | Direction.RIGHT)) > 0)
            {
                if (iY + 1 <= GridSizeY - 1)
                    iEnd = GridTransformHelper.IndexForCoord(iX, iY + 1, GridSizeX, GridSizeY);
                else
                    iEnd = GridTransformHelper.IndexForCoord(iX, iY - 1, GridSizeX, GridSizeY);    //Special case for perfect intersection with top left corner
            }
            else if ((IntersectDir & (Direction.TOP | Direction.BOTTOM)) > 0)
            {
                if (iX + 1 <= GridSizeX - 1)
                    iEnd = GridTransformHelper.IndexForCoord(iX + 1, iY, GridSizeX, GridSizeY);
                else
                    iEnd = GridTransformHelper.IndexForCoord(iX - 1, iY, GridSizeX, GridSizeY);    //Special case for perfect intersection with bottom right corner
            }/*
            else if (iX >= GridSizeX && iY >= GridSizeY)
            {
                //Special case of perfectly intersecting top right corner
                iEnd = GridTransformHelper.IndexForCoord(iX - 1, iY, GridSizeX, GridSizeY);
            }*/
            else
            {
                Debug.Fail("Unexpected fall through when detecting which line was intersected.  GridTransform::ConvexHullIntersection");
                throw new ArgumentException("Unexpected fall through when detecting which line was intersected.  GridTransform::ConvexHullIntersection");
            }

            Debug.Assert(iStart != iEnd);

            GridLineSegmentPair pair = new GridLineSegmentPair(
                mapline: new GridLineSegment(MapPoints[iStart].MappedPoint, MapPoints[iEnd].MappedPoint),
                ctrlline: new GridLineSegment(MapPoints[iStart].ControlPoint, MapPoints[iEnd].ControlPoint));

            return pair;
        }


    }
}
