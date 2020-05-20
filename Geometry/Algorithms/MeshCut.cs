//#define TRACEDELAUNAY

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using System.Diagnostics;

namespace Geometry.Meshing
{

    internal class MeshCut
    {
        public GridRectangle BoundingBox;

        public readonly CutDirection CutAxis;

        public long[] XSortedVerts;
        public long[] YSortedVerts;

        /// <summary>
        /// Used for quick Contains tests
        /// </summary>
        private HashSet<long> _AllVerts;

        /// <summary>
        /// When set to true, the XSortedVerts with equal X values are sorted by ascending Y value, otherwise by descending Y value
        /// </summary>
        public bool XSecondAxisAscending = true;

        /// <summary>
        /// When set to true, the YSortedVerts with equal Y values are sorted by ascending X value, otherwise by descending X value
        /// </summary>
        public bool YSecondAxisAscending = true;

        public long Count { get { return XSortedVerts.LongLength; } }

        public bool Contains(long value)
        {
            return _AllVerts.Contains(value);
        }

        public IReadOnlyList<long> Verticies { get { return CutAxis == CutDirection.HORIZONTAL ? XSortedVerts : YSortedVerts; } }

        public long[] SortedAlongCutAxisVertSet
        {
            get { return CutAxis == CutDirection.VERTICAL ? YSortedVerts : XSortedVerts; }
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
            get { return CutAxis == CutDirection.VERTICAL ? XSortedVerts : YSortedVerts; }
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
            get
            {
                return SortedOppositeCutAxisVertSet[key];
            }
            set
            {
                SortedOppositeCutAxisVertSet[key] = value;
            }
        }

        public int this[int key]
        {
            get
            {
                return (int)SortedOppositeCutAxisVertSet[key];
            }
            set
            {
                SortedOppositeCutAxisVertSet[key] = value;
            }
        }

        public MeshCut(long[] SortedAlongAxis, long[] SortedOppositeAxis, CutDirection cutAxis, GridRectangle boundingRect)
        {
            CutAxis = cutAxis;
            XSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedAlongAxis : SortedOppositeAxis;
            YSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedOppositeAxis : SortedAlongAxis;
            BoundingBox = boundingRect;

            _AllVerts = new HashSet<long>(SortedAlongAxis);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
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

            GridVector2 L = mesh[(int)NewSortedAlongCutAxisVertSet[nLowerHalf - 1]].Position;
            GridVector2 U = mesh[(int)NewSortedAlongCutAxisVertSet[nLowerHalf]].Position;

            double OffAxisDividingLine = cutDirection == CutDirection.HORIZONTAL ? L.X : L.Y;

            //Find the start of points that are near the dividing line
            List<int> PointsToSort = new List<int>();
            long iStart = nLowerHalf - 1;
            while(iStart >= 0)
            {
                GridVector2 p = mesh[(int)NewSortedAlongCutAxisVertSet[iStart]].Position;
                double LinePos = cutDirection == CutDirection.HORIZONTAL ? p.X : p.Y;
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
                GridVector2 p = mesh[(int)NewSortedAlongCutAxisVertSet[iEnd]].Position;
                double LinePos = cutDirection == CutDirection.HORIZONTAL ? p.X : p.Y;
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
            GridVector2[] sortPos = new GridVector2[toSort.Length];
            double[] sortVals = new double[toSort.Length];
            for(long i = iStart; i < iEnd; i++)
            {
                long iArray = i - iStart;
                toSort[iArray] = NewSortedAlongCutAxisVertSet[i];
                sortPos[iArray] = mesh[(int)toSort[iArray]].Position;
                sortVals[iArray] = cutDirection == CutDirection.HORIZONTAL ? sortPos[iArray].Y : sortPos[iArray].X;
            }

            int[] iSorted = sortVals.SortAndIndex();
            long[] correctOrder = iSorted.Select(i => toSort[i]).ToArray();

            for(long i = iStart; i < iEnd; i++)
            {
                long iArray = i - iStart;
                NewSortedAlongCutAxisVertSet[i] = correctOrder[iArray];
            }

        }

        public void SplitIntoHalves(IReadOnlyList<IVertex2D> mesh, out MeshCut LowerSubset, out MeshCut UpperSubset)
        {
            //Split the verticies into smaller groups and then merge the resulting triangulations
            CutDirection cutDirection = BoundingBox.Width > BoundingBox.Height ? CutDirection.VERTICAL : CutDirection.HORIZONTAL;

            if(this.Verticies.Count < 2)
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
            //    1, 3, 5, 4       XSorted Indicies
            //   B3, D1, E0, F4   XSorted Set

            //YSorted Indicies for Set
            // 3, 1, 4, 0      YSorted Indicies            
            //E0, D1, B3, F4, YSorted Set

            AdjustCutAxisOrderForEpsilon(mesh, cutDirection, ref NewSortedAlongCutAxisVertSet);

            //Divide verticies into two groups along the axis
            long nLowerHalf = NewSortedAlongCutAxisVertSet.LongLength / 2;
            long nUpperHalf = NewSortedAlongCutAxisVertSet.LongLength - nLowerHalf;

            long[] LowerHalfAlongAxis = new long[nLowerHalf];
            long[] UpperHalfAlongAxis = new long[nUpperHalf];
            long[] LowerHalfOppAxis = new long[LowerHalfAlongAxis.LongLength];
            long[] UpperHalfOppAxis = new long[UpperHalfAlongAxis.LongLength];

            Array.Copy(NewSortedOppositeCutAxisVertSet, LowerHalfOppAxis, LowerHalfOppAxis.LongLength);
            Array.Copy(NewSortedOppositeCutAxisVertSet, LowerHalfOppAxis.LongLength, UpperHalfOppAxis, 0, UpperHalfOppAxis.LongLength);

            //GridVector2 DivisionPoint = (mesh[(int)FirstHalfOppAxis.Last()].Position + mesh[(int)SecondHalfAlongAxis.First()].Position) / 2.0;
            GridVector2 DivisionPoint = mesh[(int)LowerHalfOppAxis.Last()].Position;

            //Divide the opposite axis sorted set into two groups as well
            long iLowerHalfAdd = 0;
            long iUpperHalfAdd = 0;

#if TRACEDELAUNAY
            Trace.WriteLine(string.Format("{0}--------{1}-------",cutDirection, DivisionPoint));
#endif
            GridVector2[] vertPosArray = NewSortedAlongCutAxisVertSet.Select(i => mesh[(int)i].Position).ToArray();
            for (long i = 0; i < NewSortedAlongCutAxisVertSet.LongLength; i++)
            {
                long iVert = NewSortedAlongCutAxisVertSet[i];
                GridVector2 vertPos = vertPosArray[i];//mesh[(int)iVert].Position;
                bool AssignToLower = false;
                if (cutDirection == CutDirection.HORIZONTAL)
                {
                    if (vertPos.Y < DivisionPoint.Y)
                    {
                        AssignToLower = true;
                    }
                    else if (vertPos.Y == DivisionPoint.Y)
                    {
                        AssignToLower = iVert == LowerHalfOppAxis.Last() || LowerHalfOppAxis.Contains(iVert);
                    }
                    else
                    {
                        AssignToLower = false;
                    }
                }
                else
                {
                    if (vertPos.X < DivisionPoint.X)
                    {
                        AssignToLower = true;
                    }
                    else if (vertPos.X == DivisionPoint.X)
                    {
                        AssignToLower = iVert == LowerHalfOppAxis.Last() || LowerHalfOppAxis.Contains(iVert);
                    }
                    else
                    {
                        AssignToLower = false;
                    }
                }

                if (AssignToLower)
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("1st <- {0}: {1}", iVert, vertPos));
#endif

                    LowerHalfAlongAxis[iLowerHalfAdd] = iVert;
                    iLowerHalfAdd += 1;
                }
                else
                {

#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("2nd <- {0}: {1}", iVert, vertPos));
#endif

                    UpperHalfAlongAxis[iUpperHalfAdd] = iVert;
                    iUpperHalfAdd += 1;
                }
            }

            GridRectangle LowerHalfBBox;
            GridRectangle UpperHalfBBox;
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                LowerHalfBBox = new GridRectangle(BoundingBox.Left, BoundingBox.Right, mesh[(int)LowerHalfOppAxis[0]].Position.Y, mesh[(int)LowerHalfOppAxis.Last()].Position.Y);
                UpperHalfBBox = new GridRectangle(BoundingBox.Left, BoundingBox.Right, mesh[(int)UpperHalfOppAxis[0]].Position.Y, mesh[(int)UpperHalfOppAxis.Last()].Position.Y);

                LowerSubset = new MeshCut(LowerHalfAlongAxis, LowerHalfOppAxis, cutDirection, LowerHalfBBox);
                UpperSubset = new MeshCut(UpperHalfAlongAxis, UpperHalfOppAxis, cutDirection, UpperHalfBBox);
            }
            else
            {
                LowerHalfBBox = new GridRectangle(mesh[(int)LowerHalfOppAxis[0]].Position.X, mesh[(int)LowerHalfOppAxis.Last()].Position.X, BoundingBox.Bottom, BoundingBox.Top);
                UpperHalfBBox = new GridRectangle(mesh[(int)UpperHalfOppAxis[0]].Position.X, mesh[(int)UpperHalfOppAxis.Last()].Position.X, BoundingBox.Bottom, BoundingBox.Top);

                LowerSubset = new MeshCut(LowerHalfAlongAxis, LowerHalfOppAxis, cutDirection, LowerHalfBBox);
                UpperSubset = new MeshCut(UpperHalfAlongAxis, UpperHalfOppAxis, cutDirection, UpperHalfBBox);
            }
#if DEBUG
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                string s = string.Format("Horizontal: Left | Right reversed {0} | {1}", LowerSubset, UpperSubset);
                Trace.WriteLineIf(mesh[(int)LowerSubset.Verticies[0]].Position.Y > mesh[(int)UpperSubset.Verticies[0]].Position.Y, s);
                Debug.Assert(mesh[(int)LowerSubset.Verticies[0]].Position.Y <= mesh[(int)UpperSubset.Verticies[0]].Position.Y, s);
            }
            else
            {
                string s = string.Format("Vertical: Left | Right reversed {0} | {1}", LowerSubset, UpperSubset);
                Trace.WriteLineIf(mesh[(int)LowerSubset.Verticies[0]].Position.X > mesh[(int)UpperSubset.Verticies[0]].Position.X, s);
                Debug.Assert(mesh[(int)LowerSubset.Verticies[0]].Position.X <= mesh[(int)UpperSubset.Verticies[0]].Position.X, s);
            }
#endif
            LowerSubset.SortSecondAxis(mesh, true);
            UpperSubset.SortSecondAxis(mesh, false);
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
            int temp;
            GridVector2 p1;
            GridVector2 p2;

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
