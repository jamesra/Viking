using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace MorphologyMesh
{
    /// <summary>
    /// A set of faces that represent a region which needs to be mapped to the adjacent section or triangulated and assigned a flat mesh
    /// </summary>
    public class MorphMeshRegion : IComparable<MorphMeshRegion>, IEquatable<MorphMeshRegion>

    {
        readonly private MorphRenderMesh ParentMesh;

        private ImmutableSortedSet<MorphMeshFace> _Faces;
        public ImmutableSortedSet<MorphMeshFace> Faces
        {
            get { return _Faces; }
        }

        public RegionType Type { get; private set; }

        public MorphMeshRegion(MorphRenderMesh mesh, IEnumerable<MorphMeshFace> faces, RegionType type)
        {

            ParentMesh = mesh;
            var f = new SortedSet<MorphMeshFace>(faces);
            _Faces = f.ToImmutableSortedSet();

            Debug.Assert(_Faces.IsEmpty == false, "Region should not have zero faces otherwise it is not really a region is it?");
            Type = type;
        }


        /// <summary>
        /// Invaginations must have only one open end.  There are times when edges are reported as invaginations when in fact they are bridges which are not true regions. 
        /// HOwever bridges have two edges that are 
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        static internal bool IsValidInvagination(MorphMeshRegion region)
        {
            Debug.Assert(region.Type == RegionType.INVAGINATION);

            IEnumerable<MorphMeshEdge> RegionEdges = region.Faces.SelectMany(f => f.Edges).Distinct().Select(key => (MorphMeshEdge)region.ParentMesh.Edges[key]);

            //We are looking for two region faces that have an edge with a non-region face or only one face (On the convex hull).  If there are two this is a bridge and not an invagination
            IEnumerable<MorphMeshEdge> CandidateEdges = RegionEdges.Where(e => e.Type != EdgeType.CONTOUR);

            List<MorphMeshEdge> ExposedEdges = CandidateEdges.Where(e => e.Faces.Count == 1 || e.Faces.Any(face => !region.Contains(face))).ToList();

            return ExposedEdges.Count <= 1;
        }

        private ImmutableSortedSet<double> _Z;
        public ImmutableSortedSet<double> ZLevel
        {
            get
            {
                if (Faces.Count == 0)
                    throw new ArgumentException("No faces in region");

                if (_Z == null)
                {
                    var builder = new SortedSet<double>();
                    var Z = this.VertPositions.Select(v => v.Z).Distinct();
                    builder.UnionWith(Z);
                    _Z = builder.ToImmutableSortedSet();
                }

                return _Z;
            }
        }

        private GridPolygon _Polygon = null;

        public GridPolygon Polygon
        {
            get
            {
                if (_Polygon != null)
                    return _Polygon;

                List<GridVector2> poly_verts = this.RegionPerimeter.Select(v => v.Position.XY()).ToList();
                _Polygon = new GridPolygon(poly_verts.EnsureClosedRing().ToArray());

                return _Polygon;
            }
        }

        private MorphMeshVertex[] _RegionPerimeter = null; //The region indicies organized so they progress in order around the perimeter of the region

        /// <summary>
        /// Returns a closed loop of verticies that define the region's perimeter
        /// </summary>
        public MorphMeshVertex[] RegionPerimeter
        {
            get
            {
                if (_RegionPerimeter != null)
                    return _RegionPerimeter;

                PolygonIndex[] polyIndicies = Verticies.Select(v => ((MorphMeshVertex)ParentMesh.Verticies[v]).PolyIndex.Value).ToArray();

                //var all_exterior_edges = this.Faces.SelectMany(f => f.Edges).Distinct().Where(e => this.ParentMesh[e].Faces.Count == 1).Select(e => ParentMesh[e]).ToList();
                var all_region_face_edges = this.Faces.SelectMany(f => f.Edges).ToList();
                var all_possible_edges = all_region_face_edges.Distinct().ToList();
                var counts = all_possible_edges.Select(e => all_region_face_edges.Count(fe => e.Equals(fe))).ToList();
                var all_exterior_edges = all_possible_edges.Where(e => all_region_face_edges.Count(fe => fe.Equals(e)) == 1).ToList();

                //Identify all of the edges that are already in the mesh as 
                //var all_exterior_edges = all_possible_edges.Where(e => this.ParentMesh.Contains(e) && ParentMesh[e].Faces.Intersect(this.Faces).Count == 1).ToList();
                var startingedge = all_exterior_edges.First();

                List<int> OrderedBoundaryVerts = new List<int>(all_exterior_edges.Count + 1)
                {
                    all_exterior_edges[0].A,
                    all_exterior_edges[0].B
                };
                all_exterior_edges.RemoveAt(0);


                while (all_exterior_edges.Count > 0)
                {
                    int FirstVertIndex = OrderedBoundaryVerts.First();
                    int LastVertIndex = OrderedBoundaryVerts.Last();

                    IEdgeKey connected_edge = all_exterior_edges.FirstOrDefault(e => e.A == LastVertIndex || e.B == LastVertIndex || e.A == FirstVertIndex || e.B == FirstVertIndex);
                    if (connected_edge == null)
                    {
#if DEBUG
                        throw new InvalidOperationException("We should always be able to find an edge to add to our perimeter until we exhaust the list of unassigned perimeter edges");
#endif
                        break; //In Release just use what we found
                    }
                    else if (connected_edge.A == LastVertIndex || connected_edge.B == LastVertIndex)
                    {
                        OrderedBoundaryVerts.Add(connected_edge.OppositeEnd(LastVertIndex));
                    }
                    else
                    {
                        OrderedBoundaryVerts.Insert(0, connected_edge.OppositeEnd(FirstVertIndex));
                    }

                    all_exterior_edges.Remove(connected_edge);
                }

                _RegionPerimeter = OrderedBoundaryVerts.Select(i => (MorphMeshVertex)ParentMesh.Verticies[i]).ToArray();

                return _RegionPerimeter;
            }
        }

        private static List<PolygonIndex[]> IdentifyContours(PolygonIndex[] polyIndicies)
        {
            //Make sure we don't have artificial jumps in the array at 0 indicies. i.e. A line that wraps around the end to the beginning of the ring
            polyIndicies = PolygonIndex.SortByRing(polyIndicies);

            List<PolygonIndex[]> listContours = new List<PolygonIndex[]>();

            List<PolygonIndex> contour = new List<PolygonIndex>
            {
                polyIndicies[0]
            };
            for (int i = 1; i < polyIndicies.Length; i++)
            {
                PolygonIndex lastCountourPoint = contour.Last();
                PolygonIndex pi = polyIndicies[i];
                //if (pi.iInnerPoly != lastCountourPoint.iInnerPoly || pi.iPoly != lastCountourPoint.iPoly)
                if (!lastCountourPoint.AreAdjacent(pi))
                {
                    listContours.Add(contour.ToArray());
                    contour = new List<PolygonIndex>
                    {
                        pi
                    };
                }
                else
                {
                    contour.Add(pi);
                }
            }

            //If we started in the middle of a contour due to the indicies wrapping around we prepend the last contour
            //to the first contour in the list
            if (contour.Last().AreAdjacent(listContours.First()[0]))
                listContours[0] = listContours[0].Union(contour).ToArray();
            else
                listContours.Add(contour.ToArray());

            return listContours;
        }

        /// <summary>
        /// Returns an open ring of points.
        /// </summary>
        /// <param name="contours"></param>
        /// <param name="PolyIndexToMeshIndex"></param>
        /// <returns></returns>
        private PolygonIndex[] ConnectContours(List<PolygonIndex[]> contours, Dictionary<PolygonIndex, int> PolyIndexToMeshIndex)
        {
            List<PolygonIndex> AssembledContour = new List<PolygonIndex>();

            PolygonIndex[] lastContour = contours[0];
            AssembledContour.AddRange(lastContour);

            GridVector2[] lastContourEndpoints = ContourEndpoints(lastContour, PolyIndexToMeshIndex);

            for (int i = 1; i < contours.Count; i++)
            {
                PolygonIndex[] Contour = contours[i];
                if (Contour.Length == 1)
                {
                    AssembledContour.AddRange(Contour);
                }
                else
                {
                    GridVector2[] Endpoints = ContourEndpoints(Contour, PolyIndexToMeshIndex);

                    GridLineSegment B = new GridLineSegment(lastContourEndpoints[1], Endpoints[0]);
                    GridLineSegment A = new GridLineSegment(lastContourEndpoints[0], Endpoints[1]);

                    //If the line crosses then we need to reverse the contour before adding it to the output
                    if (A.Intersects(in B))
                    {
                        lastContour = Contour.Reverse().ToArray();
                    }
                    else
                    {
                        lastContour = Contour;
                    }

                    AssembledContour.AddRange(lastContour);
                }

                lastContourEndpoints = ContourEndpoints(AssembledContour, PolyIndexToMeshIndex);
            }

            return AssembledContour.ToArray();
        }

        GridVector2[] ContourEndpoints(IReadOnlyList<PolygonIndex> contour, Dictionary<PolygonIndex, int> PolyIndexToMeshIndex)
        {
            int iStart = PolyIndexToMeshIndex[contour[0]];
            int iEnd = PolyIndexToMeshIndex[contour.Last()];

            return new GridVector2[]
                { this.ParentMesh.Verticies[iStart].Position.XY(),
                  this.ParentMesh.Verticies[iEnd].Position.XY() };
        }

        private int[] _Verticies = null;
        /// <summary>
        /// Return region verticies in no particular order
        /// </summary>
        public int[] Verticies
        {
            get
            {
                if (_Verticies == null)
                    _Verticies = Faces.SelectMany(f => f.iVerts).Distinct().ToArray();

                return _Verticies;
            }
        }

        public GridVector3[] VertPositions
        {
            get
            {
                return Verticies.Select(v => ParentMesh.Verticies[v].Position).ToArray();
            }
        }

        /// <summary>
        /// Return true if this regions polygons is entirely outside any polygons on the adjacent section
        /// </summary>
        /// <returns></returns>
        public bool IsExposed(MorphRenderMesh mesh)
        {
            GridPolygon[] AdjacentPolys = mesh.Polygons.Where((p, i) => this.ZLevel.Contains(mesh.PolyZ[i]) == false).ToArray();

            if (AdjacentPolys.Any(p => p.Contains(this.Polygon)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Return true if this regions polygons is entirely outside any polygons on the adjacent section
        /// </summary>
        /// <returns></returns>
        public bool IsPartlyExposed(MorphRenderMesh mesh)
        {
            GridPolygon[] AdjacentPolys = mesh.Polygons.Where((p, i) => this.ZLevel.Contains(mesh.PolyZ[i]) == false).ToArray();

            if (AdjacentPolys.Any(p => p.Intersects(this.Polygon) && !p.Contains(this.Polygon)))
            {
                return true;
            }

            return false;
        }



        public double NearestDistance(MorphMeshRegion other)
        {
            return this.Polygon.Distance(other.Polygon);
        }

        public bool Contains(IFace face)
        {
            return this.Faces.Contains(face);
        }

        public int CompareTo(MorphMeshRegion other)
        {
            if (this.Faces.Count != other.Faces.Count)
            {
                return other.Faces.Count - this.Faces.Count;
            }

            MorphMeshFace[] Mine = Faces.ToArray();
            MorphMeshFace[] Theirs = other.Faces.ToArray();

            for (int i = 0; i < this.Faces.Count; i++)
            {
                int comparison = Mine[i].CompareTo(Theirs[i]);
                if (comparison != 0)
                    return comparison;
            }

            return 0;
        }

        public bool Equals(MorphMeshRegion other)
        {
            return this.Faces.SetEquals(other.Faces);
        }

        public override string ToString()
        {
            return string.Format("Reg: {0} {1} {2}", this.Faces.First(), this.Faces.Count, this.Type.ToString());
        }
    }

}

