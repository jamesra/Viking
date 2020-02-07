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
        private readonly int[] CandidateEdges;

        /// <summary>
        /// A list of the serially linked edge constraints
        /// </summary>
        public int[] EdgeVerts;

        /// <summary>
        /// Indicates how far along the list of Edges we have added to the mesh.  i = 1 means Edge(Edges[0], Edges[1]) has been added
        /// </summary>
        public int EdgesAdded;

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

            CandidateEdges = candidateEdges;

            EdgeVerts = SelectValidEdges(CandidateEdges);
        }

        /// <summary>
        /// Of the random set of integers we were provided, figure out which ones will not intersect
        /// with other constraints if added to the mesh
        /// </summary>
        /// <param name="candidateEdges"></param>
        private int[] SelectValidEdges(int[] candidateEdges)
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

                GridLineSegment proposedSeg = mesh.ToGridLineSegment(proposedEdge);

                if (IntersectsConstrainedLine(proposedEdge, ConstrainedEdgeTree, mesh))
                    continue;

                Edge new_edge = new Edge(proposedEdge.A, proposedEdge.B);
                EdgeStart = CandidateEdges[i];
                ConstrainedEdgeTree.Add(proposedSeg.BoundingBox, new_edge);
                AddedConstraints.Add(candidateEdges[i]);
            }

            return AddedConstraints.ToArray();
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
                        .Classify(mesh.Verticies.Count < 3, "Fewer than 3 verts")
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
                value.AddContrainedEdge(new Edge(EdgeToAdd));
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
