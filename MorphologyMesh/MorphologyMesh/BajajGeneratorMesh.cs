using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Geometry;
using Geometry.Meshing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MorphologyMesh
{

    /// <summary>
    /// 
    /// Used by the Bajaj generator to represent two sets of polygons, upper and lower, regardless of actual Z levels.
    /// 
    /// MorphRenderMesh class was originally written to handle polygons at arbitrary Z levels.  However, when generating a full mesh it
    /// is possible, if annotators are trying to be difficult, to have annotations with layouts in Z like this.  That have to be grouped 
    /// into a single mesh in order to branch the mesh correctly.
    /// 
    ///  Z = 1:          A
    ///                 / \ 
    ///  Z = 2:        B   \   D
    ///                     \ /
    ///  Z = 3:              C
    ///
    /// In this case the "upper" polygons are A,D and the "lower" polygons are B,C.  Even though B & D are on the same Z level.
    /// </summary>
    public class BajajGeneratorMesh : MorphRenderMesh
    {
        internal readonly bool[] IsUpperPolygon;

        public readonly ImmutableSortedSet<int> UpperPolyIndicies;
        public readonly ImmutableSortedSet<int> LowerPolyIndicies;

        internal readonly GridPolygon[] UpperPolygons;
        internal readonly GridPolygon[] LowerPolygons;

        private List<MorphMeshRegion> _Regions = new List<MorphMeshRegion>();

        public List<MorphMeshRegion> Regions { get; private set; }

        /// <summary>
        /// An optional field that allows tracking of which annotations compose the mesh
        /// </summary>
        public MeshingGroup AnnotationGroup = null;

        public override string ToString()
        {
            string output = "";
            if(AnnotationGroup != null)
            {
                output += AnnotationGroup.ToString() + ":\n\t";
            }

            output += base.ToString();
            return output;
        }

        public BajajGeneratorMesh(IReadOnlyList<GridPolygon> polygons, IReadOnlyList<double> ZLevels, IReadOnlyList<bool> IsUpperPolygon, MeshingGroup group) : this(polygons, ZLevels, IsUpperPolygon)
        {
            AnnotationGroup = group;
        }

        public BajajGeneratorMesh(IReadOnlyList<GridPolygon> polygons, IReadOnlyList<double> ZLevels, IReadOnlyList<bool> IsUpperPolygon) : base(polygons, ZLevels)
        {
            Debug.Assert(polygons.Count == IsUpperPolygon.Count);

            this.IsUpperPolygon = IsUpperPolygon.ToArray();

            //Assign polys to sets for convienience later
            List<int> UpperPolys = new List<int>(IsUpperPolygon.Count);
            List<int> LowerPolys = new List<int>(IsUpperPolygon.Count);
            for (int i = 0; i < IsUpperPolygon.Count; i++)
            {
                if (IsUpperPolygon[i])
                    UpperPolys.Add(i);
                else
                    LowerPolys.Add(i);
            }

            UpperPolyIndicies = UpperPolys.ToImmutableSortedSet<int>();
            LowerPolyIndicies = LowerPolys.ToImmutableSortedSet<int>();

            UpperPolygons = UpperPolys.Select(i => polygons[i]).ToArray();
            LowerPolygons = LowerPolys.Select(i => polygons[i]).ToArray(); 
        }

        public GridPolygon[] GetSameLevelPolygons(PointIndex key)
        {
            return IsUpperPolygon[key.iPoly] ? UpperPolygons : LowerPolygons;
        }

        public GridPolygon[] GetAdjacentLevelPolygons(PointIndex key)
        {
            return IsUpperPolygon[key.iPoly] ? LowerPolygons : UpperPolygons;
        }

        public GridPolygon[] GetSameLevelPolygons(SliceChord sc)
        {
            return IsUpperPolygon[sc.Origin.iPoly] ? UpperPolygons : LowerPolygons;
        }

        public GridPolygon[] GetAdjacentLevelPolygons(SliceChord sc)
        {
            return IsUpperPolygon[sc.Origin.iPoly] ? LowerPolygons : UpperPolygons;
        }

        /// <summary>
        /// Add verticies at intersection points for all intersection points between polygons in the two sets. 
        /// Polygons intersecting in the same point will not have points added
        /// </summary>
        public static void AddCorrespondingVerticies(ICollection<GridPolygon> APolys, ICollection<GridPolygon> BPolys)
        {
            List<GridVector2> added_intersections;
            foreach (GridPolygon A in APolys)
            {
                foreach (GridPolygon B in BPolys)
                {
                    added_intersections = A.AddPointsAtIntersections(B);
# if DEBUG
                    foreach (GridVector2 p in added_intersections)
                    {
                        Debug.Assert(A.IsVertex(p));
                        Debug.Assert(B.IsVertex(p));
                    }
#endif
                    //B.AddPointsAtIntersections(A);
                }
            } 
        }

        /// <summary>
        /// Add verticies at intersection points for all intersection points
        /// </summary>
        /// <param name="Polys"></param>
        public static void AddCorrespondingVerticies(IReadOnlyList<GridPolygon> Polys)
        {
            List<GridVector2> added_intersections;
            foreach (var combo in Polys.CombinationPairs())
            {
                GridPolygon A = combo.A;
                GridPolygon B = combo.B;
                added_intersections = A.AddPointsAtIntersections(B);
# if DEBUG
                    foreach (GridVector2 p in added_intersections)
                    {
                        Debug.Assert(A.IsVertex(p));
                        Debug.Assert(B.IsVertex(p));
                    }
#endif
                    //B.AddPointsAtIntersections(A);
                
            }
        }

        public void IdentifyRegionsViaFaces()
        {
            this.Regions = IdentifyRegions(this);
        }

        public MorphMeshRegionGraph IdentifyRegionsViaVerticies(List<MorphMeshVertex> IncompleteVerticies)
        {
            return SecondPassRegionDetection(this, IncompleteVerticies);
        }

        /// <summary>
        /// For each vertex, 
        /// find all paths along edges without faces that can return to the that enclose triangles or quads and create faces if they don't exist
        /// </summary>
        public void CloseFaces(IEnumerable<IVertex> VertsToClose = null)
        {
            if (VertsToClose == null)
            {
                VertsToClose = this.Verticies;
            }

            foreach (var v in VertsToClose)
            {
                this.CloseFaces(v);
            }
        }

        /// <summary>
        /// For the passed vertex, identify any connected edges without two faces.  Determine if a path can be walked along edges with missing faces
        /// back to the passed vertex.  If a path exists with a length of 3 or 4 add it to the mesh.
        /// </summary>
        public void CloseFaces(IVertex vertexToClose)
        {
            //Identify edges missing faces, COUNTOUR edges only have one face to be considered complete
            List<IEdge> edges = vertexToClose.Edges.Select(key => Edges[key]).Where(e => ((MorphMeshEdge)e).FacesComplete == false).ToList();

            foreach (var edge in edges)
            {
                List<int> Face = FindCloseableFace(vertexToClose.Index, this[edge.OppositeEnd(vertexToClose.Index)], edge);
                if (Face != null)
                {
                    Debug.Assert(Face.Count == 3 || Face.Count == 4);
                    if (Face.Count == 4)
                        continue;

                    IFace f = this.CreateFace(Face);
                    if (this.Faces.Contains(f) == false)
                        this.AddFace(f);

                    if (f.iVerts.Length == 4)
                        this.SplitFace(f);
                }
            }
        }

        /// <summary>
        /// Identify if there are faces that could be created using the specified verticies
        /// </summary>
        /// <param name="TargetVert"></param>
        /// <param name="current"></param>
        /// <param name="testEdge"></param>
        /// <param name="CheckedEdges"></param>
        /// <param name="Path"></param>
        /// <returns></returns>
        private List<int> FindCloseableFace(int TargetVert, IVertex current, IEdge testEdge, SortedSet<IEdgeKey> CheckedEdges = null, Stack<int> Path = null)
        {
            if (CheckedEdges == null)
            {
                CheckedEdges = new SortedSet<IEdgeKey>();
            }

            if (Path == null)
            {
                Path = new Stack<int>();
                Path.Push(TargetVert);
            }

            //Make sure the face formed by the top three entries in the path is not already present in the mesh

            List<int> FaceTest = StackExtensions<int>.Peek(Path, 3);
            if (FaceTest.Count == 3)
            {
                if (this.Contains(new Face(FaceTest)))
                    return null;
            }

            /////////////////////////////////////////////////////////////

            CheckedEdges.Add(testEdge.Key);
            if (Path.Count > 4) //We must return only triangles or quads, and we return closed loops
                return null;

            if (current.Index == TargetVert)
            {
                return Path.ToList();
            }
            else
            {
                Path.Push(current.Index);
            }

            //Test all of the edges we have not examined yet who do not have two faces already
            List<int> ShortestFace = null;
            foreach (IEdge edge in current.Edges.Where(e => !CheckedEdges.Contains(e)).Select(e => this.Edges[e]).Where(e => ((MorphMeshEdge)e).FacesComplete == false))
            {
                List<int> Face = FindCloseableFace(TargetVert, this[edge.OppositeEnd(current.Index)], edge, new SortedSet<IEdgeKey>(CheckedEdges), new Stack<int>(Path));

                if (Face != null)
                {
                    if (ShortestFace == null)
                    {
                        ShortestFace = Face;
                    }
                    else
                    {
                        if (ShortestFace.Count > Face.Count)
                        {
                            ShortestFace = Face;
                        }
                    }
                }
            }

            if (ShortestFace != null)
            {
                return ShortestFace;
            }

            //Take this index off the stack since we did not locate a path
            Path.Pop();

            return null;
        }
    }
}
