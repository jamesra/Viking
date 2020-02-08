using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Geometry.Meshing;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTree;


namespace GeometryTests.Algorithms
{
    public class ConstrainedDelaunayModel 
    {
        public TriangulationMesh<Vertex2D> mesh;
        //public List<EdgeKey> AddedConstraints = new List<EdgeKey>();
        //public List<int> AddedConstraints = new List<int>();

        /// <summary>
        /// A list of EdgeKeys for the constraints added to the mesh so far
        /// </summary>
        public List<EdgeKey> AddedConstraintEdges
        {
            get
            {
                List<EdgeKey> edges = new List<EdgeKey>(EdgesAdded);
                for (int i = 0; i < EdgesAdded; i++)
                {
                    edges.Add(new EdgeKey(EdgeVerts[i], EdgeVerts[i + 1]));
                }

                return edges;
            }
        }
        
        /// <summary>
        /// Original input to model
        /// </summary>
        internal readonly int[] OriginalCandidateEdges;

        /// <summary>
        /// A list of the serially linked edge constraints
        /// </summary>
        public int[] EdgeVerts;

        /// <summary>
        /// Indicates how far along the list of Edges we have added to the mesh.  i = 1 means Edge(Edges[0], Edges[1]) has been added
        /// </summary>
        public int EdgesAdded;

        /// <summary>
        /// Indicates the ConstraintEdges form a closed contour at some point, though where is undefined
        /// </summary>
        public bool IsClosed
        {
            get
            {
                return EdgeVerts.Length != EdgeVerts.Distinct().Count();
            }
        }

        /// <summary>
        /// Returns the edge we should add to the mesh next and increments EdgesAdded
        /// </summary>
        /// <returns></returns>
        public IEdgeKey NextConstraint()
        {
            EdgesAdded = EdgesAdded + 1;
            if (EdgesAdded >= EdgeVerts.Length)
                return null;

            return new EdgeKey(EdgeVerts[EdgesAdded-1], EdgeVerts[EdgesAdded]);
        }

        /// <summary>
        /// Returns the edge we should add to the mesh next and increments EdgesAdded
        /// </summary>
        /// <returns></returns>
        public IEdgeKey PeekConstraint()
        {
            if (EdgesAdded >= EdgeVerts.Length-1)
                return null;

            return new EdgeKey(EdgeVerts[EdgesAdded], EdgeVerts[EdgesAdded+1]);
        }

        public ConstrainedDelaunayModel(TriangulationMesh<Vertex2D> Original, int[] candidateEdges)
        {
            mesh = Original;

            OriginalCandidateEdges = candidateEdges;

            EdgeVerts = SelectValidEdges(OriginalCandidateEdges, mesh);
        }

        public ConstrainedDelaunayModel(TriangulationMesh<Vertex2D> Original)
        {
            mesh = Original;

            //Since the verticies aren't sorted, just test the edges in order
            OriginalCandidateEdges = new int[mesh.Verticies.Count - 1].Select((v,i) => i).ToArray();

            EdgeVerts = SelectValidEdges(OriginalCandidateEdges, mesh);
        }

        /// <summary>
        /// Of the random set of integers we were provided, figure out which ones will not intersect
        /// with other constraints if added to the mesh
        /// </summary>
        /// <param name="candidateEdges"></param>
        private static int[] SelectValidEdges(int[] candidateEdges, TriangulationMesh<Vertex2D> mesh)
        {
            if (candidateEdges.Length == 0)
                return new int[0];

            List<int> AddedConstraints = new List<int>(candidateEdges.Length);
            //RTree.RTree<IEdge> rTree = mesh.GenerateEdgeRTree();
            RTree.RTree<IEdgeKey> ConstrainedEdgeTree = new RTree<IEdgeKey>();

            int EdgeStart = candidateEdges[0];
            AddedConstraints.Add(EdgeStart);
            for (int i = 1; i < candidateEdges.Length; i++)
            {
                EdgeKey proposedEdge = new EdgeKey(EdgeStart, candidateEdges[i]);

                if (TryCreateConstraint(proposedEdge, mesh, ConstrainedEdgeTree))
                {
                    EdgeStart = candidateEdges[i];
                    AddedConstraints.Add(candidateEdges[i]);
                }
            }

            //Try to add one final edge to the list to create a closed shape
            int lastVert = AddedConstraints.Last();
            for (int i = 0; i < AddedConstraints.Count-2; i++)
            {
                EdgeKey proposedEdge = new EdgeKey(EdgeStart, candidateEdges[i]);
               
                if(TryCreateConstraint(proposedEdge, mesh, ConstrainedEdgeTree))
                {
                    AddedConstraints.Add(AddedConstraints[i]);
                    break; //Once we find a single edge we can close the shape to, stop
                }
            }

            return AddedConstraints.ToArray();
        }

        /// <summary>
        /// Creates the constraint if it does not intersect any existing constraints
        /// </summary>
        /// <param name="proposedEdge"></param>
        /// <param name="mesh"></param>
        /// <param name="ConstrainedEdgeTree"></param>
        /// <returns></returns>
        private static bool TryCreateConstraint(EdgeKey proposedEdge, TriangulationMesh<Vertex2D> mesh, RTree.RTree<IEdgeKey> ConstrainedEdgeTree)
        {
            if (IntersectsConstrainedLine(proposedEdge, ConstrainedEdgeTree, mesh))
                return false;

            GridLineSegment proposedSeg = mesh.ToGridLineSegment(proposedEdge);

            Edge new_edge = new Edge(proposedEdge.A, proposedEdge.B);
            //EdgeStart = candidateEdges[i];
            ConstrainedEdgeTree.Add(proposedSeg.BoundingBox, new_edge);
            //AddedConstraints.Add(candidateEdges[i]);
            return true;
        }

        /// <summary>
        /// Return true if the proposed line segment intersects a previously added constrained edge found in rTree
        /// </summary>
        /// <param name="proposed"></param>
        /// <param name="rTree"></param>
        /// <param name="mesh"></param>
        /// <returns></returns>
        static private bool IntersectsConstrainedLine(EdgeKey proposed, RTree<IEdgeKey> rTree, TriangulationMesh<Vertex2D> mesh)
        {
            GridLineSegment seg = mesh.ToGridLineSegment(proposed);
            foreach (var intersection in rTree.IntersectionGenerator(seg.BoundingBox))
            {
                if (intersection.Equals(proposed)) //Don't test for intersecting with ourselves
                    continue;

                GridLineSegment testLine = mesh.ToGridLineSegment(intersection);
                if (seg.Intersects(testLine, true))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class ConstrainedDelaunaySpec : ICommandGenerator<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>
    {
        private static Arbitrary<uint> SizeGenerator = Arb.Default.UInt32();
        private static Arbitrary<uint> NumEdgesGenerator = Arb.Default.UInt32();
        private static Arbitrary<uint> EdgeGenerator = Arb.Default.UInt32();

        public int NumVerts { get { return InitialActual.Verticies.Count; } }
        public int NumEdges { get { return InitialModel.EdgeVerts.Length - 1; } }

        public ConstrainedDelaunaySpec(int nVerts, int nEdgesMax)
        {
            if(nEdgesMax > nVerts-1)
            {
                nEdgesMax = nVerts - 1;

                if (nEdgesMax < 0)
                    nEdgesMax = 0; 
            }

            //Generate a set of candidate edges
            int[] Edges = Arb.Default.UInt32().Generator.Sample(nVerts-1, nEdgesMax).Distinct().Select(u => (int)u).ToArray();

            InitialActual = TriangulatedMeshGenerators.RandomMesh().Sample(nVerts, 1).First();
            InitialModel = new ConstrainedDelaunayModel(InitialActual, Edges);
        }

        public ConstrainedDelaunaySpec(GridVector2[] points, int[] Edges)
        {
            InitialActual = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(points.Select(v => new Vertex2D(v, null)).ToArray());
            InitialModel = new ConstrainedDelaunayModel(InitialActual, Edges.Where(e => e < points.Length).Distinct().ToArray());
        }

        public ConstrainedDelaunaySpec(TriangulationMesh<Vertex2D> mesh)
        {
            int nEdgesMax = mesh.Edges.Count - 1;
            int nVerts = mesh.Verticies.Count;

            if (nEdgesMax > nVerts - 1)
            {
                nEdgesMax = nVerts - 1;

                if (nEdgesMax < 0)
                    nEdgesMax = 0;
            }

            //Generate a set of candidate edges
            int[] Edges = Arb.Default.UInt32().Generator.Sample(nVerts - 1, nEdgesMax).Distinct().Select(u => (int)u).ToArray();

            InitialActual = mesh;//TriangulatedMeshGenerators.RandomMesh().Sample(nVerts, 1).First();
            InitialModel = new ConstrainedDelaunayModel(InitialActual, Edges);
        }

        public ConstrainedDelaunayModel InitialModel { get; private set; }

        public TriangulationMesh<Vertex2D> InitialActual { get; private set; }

        public Gen<Command<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>> Next(ConstrainedDelaunayModel value)
        {
            //If we don't have any more items then don't return a generator
            if (value.PeekConstraint() == null)
                return Gen.Elements(new Command<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>[] { new NoOperation() });

            return Gen.Elements(new Command<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>[] { new AddConstraint(value) });
            
            //return Gen.F(value.EdgeVerts.Length - 1, Gen.Fresh<Command<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>>(() => new AddConstraint(value)));
        }

        private class NoOperation : Command<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>
        {
            public override TriangulationMesh<Vertex2D> RunActual(TriangulationMesh<Vertex2D> value)
            {
                return value;
            }

            public override ConstrainedDelaunayModel RunModel(ConstrainedDelaunayModel value)
            {
                return value;
            }

            public override Property Post(TriangulationMesh<Vertex2D> mesh, ConstrainedDelaunayModel model)
            {
                return (true).Trivial(true);
            }

            public override string ToString()
            {
                return "NoOp";
            }
        }

        private class AddConstraint : Command<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>
        {
            /// <summary>
            /// The constrained edge we will add to our model
            /// </summary>
            public IEdgeKey EdgeToAdd;

            public AddConstraint(ConstrainedDelaunayModel model)
            {
                EdgeToAdd = model.PeekConstraint();
            }

            public override Property Post(TriangulationMesh<Vertex2D> mesh, ConstrainedDelaunayModel model)
            {
                bool edgesIntersect = mesh.AnyMeshEdgesIntersect();
                bool facesCCW = DelaunayTest.AreTriangulatedFacesCCW(mesh);
                bool facesColinear = DelaunayTest.AreTriangulatedFacesColinear(mesh);
                bool vertEdges = mesh.Verticies.Count < 3 || DelaunayTest.AreTriangulatedVertexEdgesValid(mesh);
                bool HasConstrainedEdge = mesh.Contains(EdgeToAdd);
                bool facesAreTriangles = DelaunayTest.AreFacesTriangles(mesh);
                bool pass = !edgesIntersect && facesCCW && vertEdges && HasConstrainedEdge && facesAreTriangles;
                Property prop = (edgesIntersect == false).Label("Edges intersect")
                        .And(facesCCW.Label("Faces Clockwise"))
                        .And((facesColinear == false).Label("Faces colinear"))
                        .And(vertEdges.Label("Verts with 0 or 1 edges"))
                        .And(facesAreTriangles.Label("Faces aren't triangles"))
                        .And(HasConstrainedEdge.Label(string.Format("Missing Edge Constraint {0}", EdgeToAdd)))
                        .ClassifyMeshSize(mesh.Verticies.Count)
                        .Classify(model.EdgeVerts.Length < 2, "Only one edge")
                        .Label(mesh.ToString());

                return prop;
            }

            public override bool Pre(ConstrainedDelaunayModel model)
            {
                return base.Pre(model);
            }

            public override TriangulationMesh<Vertex2D> RunActual(TriangulationMesh<Vertex2D> value)
            {
                value.AddConstrainedEdge(new Edge(EdgeToAdd));
                return value;
            }

            public override ConstrainedDelaunayModel RunModel(ConstrainedDelaunayModel value)
            {
                value.NextConstraint();
                return value;
            }

            public override string ToString()
            {
                return string.Format("Add Constraint {0}", EdgeToAdd);
            }
        }
    }
}
