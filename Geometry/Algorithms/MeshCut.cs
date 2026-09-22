//#define TRACEDELAUNAY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GeometryTests")]
namespace Geometry.Meshing
{

    internal class MeshCut(long[] SortedAlongAxis, long[] SortedOppositeAxis, CutDirection cutAxis, Rectangle boundingRect)
    {
        public Rectangle BoundingBox = boundingRect;

        public readonly CutDirection CutAxis = cutAxis;

        public long[] XSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedAlongAxis : SortedOppositeAxis;
        public long[] YSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedOppositeAxis : SortedAlongAxis;

        /// <summary>
        /// Used for quick Contains tests
        /// </summary>
        private readonly HashSet<long> _AllVerts = [.. SortedAlongAxis];

        /// <summary>
        /// When set to true, the XSortedVerts with equal X values are sorted by ascending Y value, otherwise by descending Y value
        /// </summary>
        public bool XSecondAxisAscending = true;

        /// <summary>
        /// When set to true, the YSortedVerts with equal Y values are sorted by ascending X value, otherwise by descending X value
        /// </summary>
        public bool YSecondAxisAscending = true;

        public long Count => XSortedVerts.LongLength;

        public bool Contains(long value) => _AllVerts.Contains(value);

        public IReadOnlyList<long> Vertices => CutAxis == CutDirection.HORIZONTAL ? XSortedVerts : YSortedVerts;

        public long[] SortedAlongCutAxisVertSet
        {
            get => CutAxis == CutDirection.VERTICAL ? YSortedVerts : XSortedVerts;
            set
            {
                if (CutAxis == CutDirection.VERTICAL)
                {
                    YSortedVerts = value;
                }
                else
                {
                    XSortedVerts = value;
                }
            }
        }

        public long[] SortedOppositeCutAxisVertSet
        {
            get => CutAxis == CutDirection.VERTICAL ? XSortedVerts : YSortedVerts;
            set
            {
                if (CutAxis == CutDirection.VERTICAL)
                {
                    XSortedVerts = value;
                }
                else
                {
                    YSortedVerts = value;
                }
            }
        }

        public long this[long key]
        {
            get => SortedOppositeCutAxisVertSet[key];
            set => SortedOppositeCutAxisVertSet[key] = value;
        }

        public int this[int key]
        {
            get => (int)SortedOppositeCutAxisVertSet[key];
            set => SortedOppositeCutAxisVertSet[key] = value;
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            foreach (long index in SortedAlongCutAxisVertSet)
            {
                sb.AppendFormat("{0} ", index);
            }

            return sb.ToString();
        }

        /// <summary>
        /// In edge cases we'll have points at the cut axis that within an epsilon distance along the cut axis. 
        /// For example:
        ///     A
        ///     |
        ///     B
        ///     |
        ///     C
        /// 
        /// Before this function was added we'd have A & C sort into one set and B sort into the other half.  This function groups all points within an epsilon distance
        /// of the cut and sorts them along the 2nd axis correctly. 
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="cutDirection"></param>
        /// <param name="NewSortedAlongCutAxisVertSet"></param>
        private static void AdjustCutAxisOrderForEpsilon(IReadOnlyList<IVertex2D> mesh, CutDirection cutDirection, ref long[] NewSortedAlongCutAxisVertSet)
        {
            long nLowerHalf = NewSortedAlongCutAxisVertSet.LongLength / 2;
            long nUpperHalf = NewSortedAlongCutAxisVertSet.LongLength - nLowerHalf;

            Vector2 L = mesh[(int)NewSortedAlongCutAxisVertSet[nLowerHalf - 1]].Position;
            Vector2 U = mesh[(int)NewSortedAlongCutAxisVertSet[nLowerHalf]].Position;

            double OffAxisDividingLine = cutDirection == CutDirection.HORIZONTAL ? L.Y : L.X;

            //Find the start of points that are near the dividing line
            List<int> PointsToSort = [];
            long iStart = nLowerHalf - 1;
            while (iStart >= 0)
            {
                Vector2 p = mesh[(int)NewSortedAlongCutAxisVertSet[iStart]].Position;
                double LinePos = cutDirection == CutDirection.HORIZONTAL ? p.Y : p.X;
                if (Math.Abs(LinePos - OffAxisDividingLine) < Global.Epsilon)
                    iStart -= 1;
                else
                {
                    iStart += 1;
                    break;
                }
            }

            if (iStart < 0)
                iStart = 0;

            //Find the end of points that are near the dividing line
            long iEnd = nLowerHalf - 1;
            while (iEnd < NewSortedAlongCutAxisVertSet.Length)
            {
                Vector2 p = mesh[(int)NewSortedAlongCutAxisVertSet[iEnd]].Position;
                double LinePos = cutDirection == CutDirection.HORIZONTAL ? p.Y : p.X;
                if (Math.Abs(LinePos - OffAxisDividingLine) < Global.Epsilon)
                    iEnd += 1;
                else
                {
                    iEnd -= 1;
                    break;
                }
            }

            //If only one point is on the dividing line we are done
            if (iEnd - iStart <= 1)
                return;

            //OK, sort the points that we know are on the dividing line
            long[] toSort = new long[iEnd - iStart];
            Vector2[] sortPos = new Vector2[toSort.Length];
            double[] sortVals = new double[toSort.Length];
            for (long i = iStart; i < iEnd; i++)
            {
                long iArray = i - iStart;
                toSort[iArray] = NewSortedAlongCutAxisVertSet[i];
                sortPos[iArray] = mesh[(int)toSort[iArray]].Position;
                sortVals[iArray] = cutDirection == CutDirection.HORIZONTAL ? sortPos[iArray].X : sortPos[iArray].Y;
            }

            int[] iSorted = sortVals.SortAndIndex();
            long[] correctOrder = [.. iSorted.Select(i => toSort[i])];

            for (long i = iStart; i < iEnd; i++)
            {
                long iArray = i - iStart;
                NewSortedAlongCutAxisVertSet[i] = correctOrder[iArray];
            }

        }

        public void SplitIntoHalves(IReadOnlyList<IVertex2D> mesh, out MeshCut LowerSubset, out MeshCut UpperSubset, CutDirection cutDirection = CutDirection.NONE)
        {
            //Split the verticies into smaller groups and then merge the resulting triangulations
            bool chosenAxis = cutDirection == CutDirection.NONE;
            if (chosenAxis)
            {
                cutDirection = BoundingBox.Width > BoundingBox.Height ? CutDirection.VERTICAL : CutDirection.HORIZONTAL;
            }

            if (this.Vertices.Count < 2)
            {
                throw new ArgumentException("Cannot cut zero or one verticies.");
            }

            long[] NewSortedAlongCutAxisVertSet;
            long[] NewSortedOppositeCutAxisVertSet;

            bool AxisAscending;

            //Sort our verticies according to the new direction
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                //Use the mesh's ordering arrays to determine the new sorted vertex order
                NewSortedAlongCutAxisVertSet = XSortedVerts;
                NewSortedOppositeCutAxisVertSet = YSortedVerts;
                AxisAscending = XSecondAxisAscending;
            }
            else
            {
                NewSortedAlongCutAxisVertSet = YSortedVerts;
                NewSortedOppositeCutAxisVertSet = XSortedVerts;
                AxisAscending = YSecondAxisAscending;
            }



            //TODO: I'm 99% certain there is a way to get the verts sorted on the new axis just by indexing the arrays, but it is late and I'm not seeing it.  
            //These are the notes I wrote trying to figure it out:
            //A,B,C,D,E,F X Values
            //0,1,2,3,4,5 Y Values

            //B3,A2,C5,D1,F4,E0 Mesh Verts

            // 1, 0, 2, 3, 5, 4 Verts SortedOnX
            // 3, 2, 5, 1, 4, 0 Verts SortedOnY

            // 0, 3, 4, 5(B3, D1, F4, E0) Sample Index Set

            //XSorted Indices for Set
            //    1, 3, 5, 4       XSorted Indices
            //   B3, D1, E0, F4   XSorted Set

            //YSorted Indices for Set
            // 3, 1, 4, 0      YSorted Indices            
            //E0, D1, B3, F4, YSorted Set

            AdjustCutAxisOrderForEpsilon(mesh, cutDirection, ref NewSortedAlongCutAxisVertSet);

            //Divide verticies into two groups along the axis
            long nLowerHalf = NewSortedAlongCutAxisVertSet.LongLength / 2;

            List<long> LowerHalfAlongAxis = new((int)nLowerHalf);
            List<long> UpperHalfAlongAxis = new((int)(NewSortedAlongCutAxisVertSet.LongLength - nLowerHalf));
            List<long> LowerHalfOppAxis = new((int)nLowerHalf);
            List<long> UpperHalfOppAxis = new((int)(NewSortedOppositeCutAxisVertSet.LongLength - nLowerHalf));

            Vector2 DivisionPoint = mesh[(int)NewSortedOppositeCutAxisVertSet[nLowerHalf - 1]].Position;

            long iLowerHalfAdd = 0;
            long iUpperHalfAdd = 0;

#if TRACEDELAUNAY
            Trace.WriteLine(string.Format("{0}--------{1}-------",cutDirection, DivisionPoint));
#endif
            Vector2[] vertPosArray = [.. NewSortedAlongCutAxisVertSet.Select(i => mesh[(int)i].Position)];

            Vector2 nudgedDivisionPoint = DivisionPoint;

            for (long i = 0; i < NewSortedAlongCutAxisVertSet.LongLength; i++)
            {
                long iVert = NewSortedAlongCutAxisVertSet[i];
                Vector2 vertPos = vertPosArray[i];//mesh[(int)iVert].Position;
                bool AssignToLower = AssignVertexToLowerHalf(cutDirection, vertPos, DivisionPoint, ref nudgedDivisionPoint);

                if (AssignToLower)
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("1st <- {0}: {1}", iVert, vertPos));
#endif

                    LowerHalfAlongAxis.Add(iVert);
                    iLowerHalfAdd += 1;
                }
                else
                {

#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("2nd <- {0}: {1}", iVert, vertPos));
#endif

                    UpperHalfAlongAxis.Add(iVert);
                    iUpperHalfAdd += 1;
                }
            }

            for (long i = 0; i < NewSortedOppositeCutAxisVertSet.LongLength; i++)
            {
                long iVert = NewSortedOppositeCutAxisVertSet[i];
                if (LowerHalfAlongAxis.Contains(iVert))
                    LowerHalfOppAxis.Add(iVert);
                else
                    UpperHalfOppAxis.Add(iVert);
            }

            //Every vertex on one side means this axis cannot separate the set, which happens when the whole set
            //shares the division coordinate.  The other axis usually can, so try it before resorting to a split
            //by index, which does not separate the halves geometrically.
            if ((LowerHalfAlongAxis.Count == 0 || UpperHalfAlongAxis.Count == 0) && chosenAxis)
            {
                SplitIntoHalves(mesh, out LowerSubset, out UpperSubset,
                    cutDirection == CutDirection.HORIZONTAL ? CutDirection.VERTICAL : CutDirection.HORIZONTAL);
                return;
            }

            if (LowerHalfAlongAxis.Count == 0 || UpperHalfAlongAxis.Count == 0)
            {
                LowerHalfAlongAxis.Clear();
                UpperHalfAlongAxis.Clear();
                LowerHalfOppAxis.Clear();
                UpperHalfOppAxis.Clear();

                for (long i = 0; i < NewSortedAlongCutAxisVertSet.LongLength; i++)
                {
                    long iVert = NewSortedAlongCutAxisVertSet[i];
                    if (i < nLowerHalf)
                    {
                        LowerHalfAlongAxis.Add(iVert);
                    }
                    else
                    {
                        UpperHalfAlongAxis.Add(iVert);
                    }
                }

                var lowerSet = new HashSet<long>(LowerHalfAlongAxis);
                for (long i = 0; i < NewSortedOppositeCutAxisVertSet.LongLength; i++)
                {
                    long iVert = NewSortedOppositeCutAxisVertSet[i];
                    if (lowerSet.Contains(iVert))
                        LowerHalfOppAxis.Add(iVert);
                    else
                        UpperHalfOppAxis.Add(iVert);
                }

                iLowerHalfAdd = LowerHalfAlongAxis.Count;
                iUpperHalfAdd = UpperHalfAlongAxis.Count;
            }

            Debug.Assert(iLowerHalfAdd == LowerHalfAlongAxis.Count);
            Debug.Assert(iUpperHalfAdd == UpperHalfAlongAxis.Count);

            Rectangle LowerHalfBBox;
            Rectangle UpperHalfBBox;
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                /*
                if (BoundingBox.Bottom == nudgedDivisionPoint.Y || BoundingBox.Top == nudgedDivisionPoint.Y)
                {
                    SplitIntoHalves(mesh,  out LowerSubset, out UpperSubset, CutDirection.VERTICAL);
                    return;
                }*/
                double[] borders = [BoundingBox.Bottom, nudgedDivisionPoint.Y, BoundingBox.Top];
                Array.Sort<double>(borders);
                LowerHalfBBox = new Rectangle(BoundingBox.Left, BoundingBox.Right, borders[0], borders[1]);
                UpperHalfBBox = new Rectangle(BoundingBox.Left, BoundingBox.Right, borders[1], borders[2]);

                LowerSubset = new MeshCut([.. LowerHalfAlongAxis], [.. LowerHalfOppAxis], cutDirection, LowerHalfBBox);
                UpperSubset = new MeshCut([.. UpperHalfAlongAxis], [.. UpperHalfOppAxis], cutDirection, UpperHalfBBox);
            }
            else
            {
                /*
                if (BoundingBox.Left == nudgedDivisionPoint.X || BoundingBox.Right == nudgedDivisionPoint.X)
                {
                    SplitIntoHalves(mesh, out LowerSubset, out UpperSubset, CutDirection.HORIZONTAL);
                    return;
                }
                */
                double[] borders = [BoundingBox.Left, nudgedDivisionPoint.X, BoundingBox.Right];
                Array.Sort<double>(borders);
                LowerHalfBBox = new Rectangle(borders[0], borders[1], BoundingBox.Bottom, BoundingBox.Top);
                UpperHalfBBox = new Rectangle(borders[1], borders[2], BoundingBox.Bottom, BoundingBox.Top);

                LowerSubset = new MeshCut([.. LowerHalfAlongAxis], [.. LowerHalfOppAxis], cutDirection, LowerHalfBBox);
                UpperSubset = new MeshCut([.. UpperHalfAlongAxis], [.. UpperHalfOppAxis], cutDirection, UpperHalfBBox);
            }
#if DEBUG
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                string s = string.Format("Horizontal: Left | Right reversed {0} | {1}", LowerSubset, UpperSubset);
                Trace.WriteLineIf(mesh[(int)LowerSubset.Vertices[0]].Position.Y > mesh[(int)UpperSubset.Vertices[0]].Position.Y, s);
                Debug.Assert(mesh[(int)LowerSubset.Vertices[0]].Position.Y <= mesh[(int)UpperSubset.Vertices[0]].Position.Y, s);
            }
            else
            {
                string s = string.Format("Vertical: Left | Right reversed {0} | {1}", LowerSubset, UpperSubset);
                Trace.WriteLineIf(mesh[(int)LowerSubset.Vertices[0]].Position.X > mesh[(int)UpperSubset.Vertices[0]].Position.X, s);
                Debug.Assert(mesh[(int)LowerSubset.Vertices[0]].Position.X <= mesh[(int)UpperSubset.Vertices[0]].Position.X, s);
            }
#endif
            LowerSubset.SortSecondAxis(mesh, true);
            UpperSubset.SortSecondAxis(mesh, false);
        }

        private static bool AssignVertexToLowerHalf(
            CutDirection cutDirection,
            Vector2 vertPos,
            Vector2 divisionPoint,
            ref Vector2 nudgedDivisionPoint)
        {
            //Verticies sharing the division coordinate all belong to the same half.  Splitting them by index
            //instead leaves the halves interleaved along the division line rather than separated by it, and the
            //merge step then builds an edge inside one half that runs exactly through a vertex of the other -
            //an edge no triangulation can contain.  Contour and medial axis coordinates are rounded onto shared
            //values, so such runs are routine here rather than exotic.
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                if (Math.Abs(vertPos.Y - divisionPoint.Y) < Global.Epsilon)
                {
                    nudgedDivisionPoint = new Vector2(nudgedDivisionPoint.X, Math.Max(vertPos.Y, divisionPoint.Y));
                    return true;
                }

                return vertPos.Y < divisionPoint.Y;
            }

            if (Math.Abs(vertPos.X - divisionPoint.X) < Global.Epsilon)
            {
                nudgedDivisionPoint = new Vector2(Math.Max(vertPos.X, divisionPoint.X), nudgedDivisionPoint.Y);
                return true;
            }

            return vertPos.X < divisionPoint.X;
        }

        /// <summary>
        /// //Assuming the points are sorted along the cut axis already, the secondary sorting axis is correct for whether the half is above or below the cut line:
        ///
        ///      1 -- 5
        ///      |    |
        ///      2 -- 6
        /// ---- |    | -- cut line --- 
        ///      3 -- 7
        ///      |    |
        ///      4 -- 8
        ///
        ///  After cutting, the Y Sorting along the X axis (for points with the same X value) for each set should be:
        ///
        ///      2 -- 6
        ///      |    |
        ///      1 -- 5
        /// ---- |    | -- cut line --- 
        ///      3 -- 7
        ///      |    |
        ///      4 -- 8
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="ascending"></param>
        private void SortSecondAxis(IReadOnlyList<IVertex2D> mesh, bool ascending = true)
        {
            int v1;
            int v2;
            //int temp;
            Vector2 p1;
            Vector2 p2;

            if (this.CutAxis == CutDirection.HORIZONTAL)
            {
                XSecondAxisAscending = ascending;
                for (int i = 0; i < this.XSortedVerts.LongLength - 1; i++)
                {
                    v1 = (int)XSortedVerts[i];
                    p1 = mesh[v1].Position;

                    for (int j = i + 1; j < this.XSortedVerts.LongLength; j++)
                    {
                        v2 = (int)XSortedVerts[j];
                        p2 = mesh[v2].Position;

                        //Check if the first axis isn't equal, if it isn't then bail on this loop
                        if (p1.X != p2.X)
                        {
                            break;
                        }

                        bool swap = ascending ? p1.Y > p2.Y : p2.Y > p1.Y;

                        if (swap)
                        {
                            XSortedVerts[i] = v2;
                            XSortedVerts[j] = v1;
                            v1 = v2;
                            p1 = p2;
                            continue;
                        }

                        //continue checking while the cut axis value remains equal
                    }
                }
            }
            else
            {
                YSecondAxisAscending = ascending;
                for (int i = 0; i < this.YSortedVerts.LongLength - 1; i++)
                {
                    v1 = (int)YSortedVerts[i];
                    p1 = mesh[v1].Position;

                    for (int j = i + 1; j < this.YSortedVerts.LongLength; j++)
                    {
                        v2 = (int)YSortedVerts[j];
                        p2 = mesh[v2].Position;

                        //Check if the first axis isn't equal, if it isn't then bail on this loop
                        if (p1.Y != p2.Y)
                        {
                            break;
                        }

                        bool swap = ascending ? p1.X > p2.X : p2.X > p1.X;

                        if (swap)
                        {
                            YSortedVerts[i] = v2;
                            YSortedVerts[j] = v1;
                            v1 = v2;
                            p1 = p2;
                            continue;
                        }

                        //continue checking while the cut axis value remains equal
                    }
                }
            }
        }

    }
}
