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
        internal override bool[] IsUpperPolygon { get { return Topology.IsUpper; } }

        public ImmutableSortedSet<int> UpperPolyIndicies { get { return Topology.UpperPolyIndicies; } }
        public ImmutableSortedSet<int> LowerPolyIndicies { get { return Topology.LowerPolyIndicies; } }

        internal GridPolygon[] UpperPolygons { get { return Topology.UpperPolygons; } }
        internal GridPolygon[] LowerPolygons { get { return Topology.LowerPolygons; } }

        private List<MorphMeshRegion> _Regions = new List<MorphMeshRegion>();

        public List<MorphMeshRegion> Regions { get; private set; }

        /// <summary>
        /// An optional field that allows tracking of which annotations compose the mesh
        /// </summary>
        public SliceTopology Topology;

        /// <summary>
        /// An optional field that allows tracking of which annotations compose the mesh
        /// </summary>
        public Slice Slice;

        public override string ToString()
        {
            string output = "";
            if(Slice != null)
            {
                output += Slice.ToString() + ":\n\t";
            }

            output += base.ToString();
            return output;
        }

        public BajajGeneratorMesh(SliceTopology topology, Slice slice = null) : base(topology.Polygons, topology.PolyZ, topology.IsUpper)
        {
            Topology = topology;
            Slice = slice;
        }

        public BajajGeneratorMesh(IReadOnlyList<GridPolygon> polygons, IReadOnlyList<double> ZLevels, IReadOnlyList<bool> IsUpperPolygon) : this(new SliceTopology(polygons, IsUpperPolygon, ZLevels))
        {

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


        public void IdentifyRegionsViaFaces()
        {
            this.Regions = IdentifyRegions(this);
        }

        public MorphMeshRegionGraph IdentifyRegionsViaVerticies(List<MorphMeshVertex> IncompleteVerticies)
        {
            return SecondPassRegionDetection(this, IncompleteVerticies);
        }

        public GridVector2 CalculateAverageVertexPositionXY()
        {
            List<GridVector2> points = new List<GridVector2>(this.Verticies.Count);

            var groups = this.Verticies.GroupBy(v => v.Corresponding.HasValue);
            foreach(var g in groups)
            {
                if(g.Key == true)
                {
                    var UniquePoints = g.Select(v => v.Position.XY()).Distinct();
                    points.AddRange(UniquePoints);
                }
                else
                {
                    points.AddRange(g.Select(v => v.Position.XY()));
                }
            }

            return points.Average();
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

        /// <summary>
        /// Flip the winding of any faces that point internally to the slice.  Ensures correct lighting.
        /// </summary>
        public void EnsureFacesHaveExternalNormals()
        {
            MorphMeshFace[] faces = this.MorphFaces.Where(f => f.NormalIsKnownCorrect == false).ToArray();
            for (int i = 0; i < faces.Length; i++)
            {
                MorphMeshFace f = faces[i];
                MorphMeshVertex[] verts = this[f.iVerts].ToArray();
                if (verts.Any(v => v.MedialAxisIndex.HasValue))
                {
                    //Medial axis verts are caps and always have normals that point either up or down.  They should be set correctly at creation.
                    continue;
                }

                if (this.FaceHasCCWWinding(f))
                    this.ReverseFace(f);

                f.NormalIsKnownCorrect = true;
            }

        }

        /// <summary>
        /// Return true if the face has CCW winding when viewed from the exterior of the mesh
        /// </summary>
        public bool FaceHasCCWWinding(IFace f)
        {
            MorphMeshVertex[] verts = this[f.iVerts].ToArray();

            GridVector3 n = this.Normal(f);
            GridVector2 face_center;

            bool CheckAgainstUpperPolygons; //True if we check if the centroid is contained in upper polygons, false if centroid needs to be checked against lower polygons
            //Check if the normal is oriented up or down.  If it is up, then check that the face centroid is not contained within the upper polygons, and vice versa.
            if (n.Z == 0)
            {
                //Todo: Special case
                if(f.IsTriangle())
                {
                    //First find the vertex that is not part of the corresponding pair that created this face.  Note that corresponding verts can be adjacent within a polygon,
                    //so if the vertex is corresponding it could stil be the extra vertex of the triangle if its corresponding vertex is not part of the face.
                    MorphMeshVertex noncorresponding = verts.Where(v => v.Corresponding.HasValue == false || f.iVerts.Contains(v.Corresponding.Value) == false).First();
                    int iNonCorresponding = Array.IndexOf(verts, noncorresponding);
                    bool NonCorrespondingIsUpper = IsUpperPolygon[noncorresponding.PolyIndex.Value.iPoly];

                    InfiniteSequentialIndexSet faceIndexer = new InfiniteSequentialIndexSet(0, f.iVerts.Length, 0);

                    MorphMeshVertex nextVert = verts[faceIndexer[iNonCorresponding + 1]];
                    MorphMeshVertex prevVert = verts[faceIndexer[iNonCorresponding - 1]];
                    //Find the line segment between the two adjacent verts on the same Z level
                    GridLineSegment seg;
                    bool output; 
                    if (nextVert.PolyIndex.Value == noncorresponding.PolyIndex.Value.Next)
                    {
                        output = NonCorrespondingIsUpper == false;
                        //seg = new GridLineSegment(noncorresponding.Position.XY(), verts[faceIndexer[iNonCorresponding + 1]].Position.XY());
                    }
                    else if(nextVert.PolyIndex.Value == noncorresponding.PolyIndex.Value.Previous)
                    {
                        output = NonCorrespondingIsUpper;
                    }
                    else if (prevVert.PolyIndex.Value == noncorresponding.PolyIndex.Value.Previous)
                    {
                        output = NonCorrespondingIsUpper == false;
                    }
                    else// if (prevVert.PolyIndex.Value == noncorresponding.PolyIndex.Value.Next)
                    {
                        output = NonCorrespondingIsUpper;
                    }
                    
                    return noncorresponding.PolyIndex.Value.IsInner ? !output : output;
                }
                else
                {
                    return true; //Not implemented
                } 
            }
            else if (n.Z < 0)
            {
                CheckAgainstUpperPolygons = false;
                face_center = GetCentroid(f);
            }
            else //n.Z > 0
            {
                CheckAgainstUpperPolygons = true;
                face_center = GetCentroid(f); 
            }

            if(CheckAgainstUpperPolygons == false)
            { 
                if (this.LowerPolygons.Any(p => p.ContainsExt(face_center) == OverlapType.CONTAINED))
                    return false;

                return true;
            }
            else
            { 
                if (this.UpperPolygons.Any(p => p.ContainsExt(face_center) == OverlapType.CONTAINED))
                    return false;

                return true;
            }
            /*
            MorphMeshVertex[] verts = this[f.iVerts].ToArray();

            //GridVector2 face_center = GetCentroid(f);
            GridVector2[] positions = verts.Select(v => v.Position.XY()).Distinct().ToArray();

            if (positions.Length < 3)
                return true; //Not implemented

            return positions.AreClockwise() == false;
            */
        }
    }
}
