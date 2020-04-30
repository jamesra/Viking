using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Geometry.Meshing;
using Geometry.JSON;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTree;


namespace GeometryTests.Algorithms
{
    public class ConstrainedDelaunayModel
    {
        public TriangulationMesh<IVertex2D> mesh;
        //public List<EdgeKey> AddedConstraints = new List<EdgeKey>();
        //public List<int> AddedConstraints = new List<int>();

        /// <summary>
        /// A list of all constraints this model plans to add to the mesh
        /// </summary>
        public List<EdgeKey> ConstraintEdges;

        /// <summary>
        /// A list of EdgeKeys for the constraints added to the mesh so far
        /// </summary>
        public List<EdgeKey> AddedConstraintEdges
        {
            get
            {
                return ConstraintEdges.GetRange(0, EdgesAdded);
            }
        }

        /// <summary>
        /// Original input to model
        /// </summary>
        internal readonly int[] OriginalCandidateEdges;

        /// <summary>
        /// A list of the serially linked edge constraints
        /// </summary>
        //public int[] EdgeVerts;

        /// <summary>
        /// Indicates how far along the list of Edges we have added to the mesh.  i = 1 means Edge(Edges[0], Edges[1]) has been added
        /// </summary>
        public int EdgesAdded = 0;

        /// <summary>
        /// Indicates the ConstraintEdges form a closed contour at some point, though where is undefined
        /// </summary>
        public bool IsClosed
        {
            get
            {
                if (ConstraintEdges.Count < 3)
                    return false;

                IEdgeKey start = ConstraintEdges.First();
                IEdgeKey finish = ConstraintEdges.Last();

                return (start.A == finish.A ||
                   start.B == finish.A ||
                   start.A == finish.B ||
                   start.B == finish.B);
            }
        }

        public override string ToString()
        {
            return string.Format("Model {0} with {1} constraints\n{2}",
                mesh.ToString(), ConstraintEdges.Count,
                new string(mesh.Verticies.Select(v => v.Position).ToArray().ToJSON().Where(c => c != '\n' && c != '\r').ToArray()));
        }

        /// <summary>
        /// Returns the edge we should add to the mesh next and increments EdgesAdded
        /// </summary>
        /// <returns></returns>
        public IEdgeKey NextConstraint()
       {
            if (EdgesAdded >= ConstraintEdges.Count)
                return null;

            IEdgeKey nextEdge = ConstraintEdges[EdgesAdded];
            EdgesAdded = EdgesAdded + 1;

            return nextEdge;
        }

        /// <summary>
        /// Returns the edge we should add to the mesh next and increments EdgesAdded
        /// </summary>
        /// <returns></returns>
        public IEdgeKey PeekConstraint()
        {
            if (EdgesAdded >= ConstraintEdges.Count)
                return null;

            return ConstraintEdges[EdgesAdded];
        }

        public ConstrainedDelaunayModel(TriangulationMesh<IVertex2D> Original, int[] candidateEdges)
        {
            mesh = Original;

            OriginalCandidateEdges = candidateEdges;

            int[] EdgeVerts = SelectValidEdges(OriginalCandidateEdges, mesh);
            this.ConstraintEdges = CreateEdges(EdgeVerts);
        }

        public ConstrainedDelaunayModel(TriangulationMesh<IVertex2D> Original)
        {
            mesh = Original;

            //Since the verticies aren't sorted, just test the edges in order
            OriginalCandidateEdges = new int[mesh.Verticies.Count - 1].Select((v, i) => i).ToArray();

            int[] EdgeVerts = SelectValidEdges(OriginalCandidateEdges, mesh);
            this.ConstraintEdges = CreateEdges(EdgeVerts);
        }

        public ConstrainedDelaunayModel(TriangulationMesh<IVertex2D> Original, List<EdgeKey> constraints)
        {
            mesh = Original;

            //Since the verticies aren't sorted, just test the edges in order
            if (mesh.Verticies.Count < 2)
            {
                OriginalCandidateEdges = new int[0];
            }
            else
            {
                OriginalCandidateEdges = new int[mesh.Verticies.Count - 1].Select((v, i) => i).ToArray();
            }
             
            this.ConstraintEdges = constraints;
        }

        public ConstrainedDelaunayModel Clone()
        {
            var output = new ConstrainedDelaunayModel(this.mesh.Clone(), this.ConstraintEdges);
            return output;
        }

        private List<EdgeKey> CreateEdges(int[] edge_seq)
        {
            List<EdgeKey> edges = new List<EdgeKey>(edge_seq.Length);
            for (int i = 0; i < edge_seq.Length-1; i++)
            {
                edges.Add(new EdgeKey(edge_seq[i], edge_seq[i + 1]));
            }

            return edges;
        }

        /// <summary>
        /// Of the random set of integers we were provided, figure out which ones will not intersect
        /// with other constraints if added to the mesh
        /// </summary>
        /// <param name="candidateEdges"></param>
        private static int[] SelectValidEdges(int[] candidateEdges, TriangulationMesh<IVertex2D> mesh)
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
        private static bool TryCreateConstraint(EdgeKey proposedEdge, TriangulationMesh<IVertex2D> mesh, RTree.RTree<IEdgeKey> ConstrainedEdgeTree)
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
        static private bool IntersectsConstrainedLine(EdgeKey proposed, RTree<IEdgeKey> rTree, TriangulationMesh<IVertex2D> mesh)
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

    public class ConstrainedDelaunaySpec : ICommandGenerator<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>
    {
        private static Arbitrary<uint> SizeGenerator = Arb.Default.UInt32();
        private static Arbitrary<uint> NumEdgesGenerator = Arb.Default.UInt32();
        private static Arbitrary<uint> EdgeGenerator = Arb.Default.UInt32();

        public int NumVerts { get { return InitialActual.Verticies.Count; } }
        public int NumEdges { get { return InitialModel.ConstraintEdges.Count; } }

        private static int NextID = 0;
        private int ID = 0;
        /// <summary>
        /// False if any of the test passes failed
        /// </summary>
        public bool Pass = true;

        private ConstrainedDelaunayModel OriginalModel;
        private TriangulationMesh<IVertex2D> OriginalMesh;

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

            OriginalMesh = TriangulatedMeshGenerators.RandomMesh().Sample(nVerts, 1).First();
            OriginalModel = new ConstrainedDelaunayModel(InitialActual, Edges);

            ID = NextID;
            NextID = NextID + 1;
            //Trace.WriteLine(string.Format("New Spec {0}:{1}", ID, InitialModel));
        }

        public ConstrainedDelaunaySpec(ConstrainedDelaunayModel model)
        {
            OriginalMesh = model.mesh;//GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(model.mesh.Verticies.Select(v => new Vertex2D(v.Position)).ToArray());
            if (TriangulatedMeshGenerators.OnProgress != null)
                TriangulatedMeshGenerators.OnProgress(InitialActual);

            OriginalModel = model;

            ID = NextID;
            NextID = NextID + 1;
            //Trace.WriteLine(string.Format("New Spec {0}:{1}", ID, InitialModel));
        }

        public ConstrainedDelaunaySpec(GridVector2[] points, int[] Edges)
        {
            OriginalMesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(points.Select(v => new Vertex2D(v, null)).ToArray());
            if (TriangulatedMeshGenerators.OnProgress != null)
                TriangulatedMeshGenerators.OnProgress(InitialActual);

            OriginalModel = new ConstrainedDelaunayModel(InitialActual, Edges.Where(e => e < points.Length).Distinct().ToArray());

            ID = NextID;
            NextID = NextID + 1;
            //Trace.WriteLine(string.Format("New Spec {0}:{1}", ID, InitialModel));
        }

        public ConstrainedDelaunaySpec(TriangulationMesh<IVertex2D> mesh)
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

            OriginalMesh = mesh;//TriangulatedMeshGenerators.RandomMesh().Sample(nVerts, 1).First();
            OriginalModel = new ConstrainedDelaunayModel(InitialActual, Edges);
        }

        public ConstrainedDelaunayModel InitialModel { get { return OriginalModel.Clone(); } }

        public TriangulationMesh<IVertex2D> InitialActual { get { return OriginalMesh.Clone(); } }

        public Gen<Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>> Next(ConstrainedDelaunayModel value)
        {
            //If we don't have any more items then don't return a generator
            Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel> result;

            if (value.PeekConstraint() == null)
                result = new NoOperation();
                //return Gen.Elements(new Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>[] { });
                //return Gen.Elements(new Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>[] { new NoOperation() });
            else
                result = new AddConstraint(value);

            //Trace.WriteLine(string.Format("Yield {0} from {1}", result, ID));

            return Gen.Constant<Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>>(result);
                            
            //return Gen.Elements(new Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>[] { new AddConstraint(value) });

            /*Gen.Sized(size => Gen.ListOf(size > value.ConstraintEdges.Count < )
            
            List<Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>> listCommands = new List<Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>>(value.ConstraintEdges.Count);

            foreach(IEdgeKey key in value.ConstraintEdges)
            {
                listCommands.Add(new AddConstraint(value));
            }

            return Gen.(listCommands);
            */
            
            
            //return Gen.F(value.EdgeVerts.Length - 1, Gen.Fresh<Command<TriangulationMesh<Vertex2D>, ConstrainedDelaunayModel>>(() => new AddConstraint(value)));
        }
        /*
        public static IEnumerable<Command<TriangulationMesh<IVertex2D> Shrinker(Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel> values)
        {
            List<Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>> listOutput = values.Where(v => v as NoOperation == null).ToList();

            for(int i = 0; i < listOutput.Count; i++)
            {
                var listCopy = listOutput.ToList();
                listCopy.RemoveAt(i);
                yield return listCopy;
            }
        }*/

        private class NoOperation : Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>
        {
            public override TriangulationMesh<IVertex2D> RunActual(TriangulationMesh<IVertex2D> value)
            {
                return value;
            }

            public override ConstrainedDelaunayModel RunModel(ConstrainedDelaunayModel value)
            {
                return value;
            }

            public override bool Pre(ConstrainedDelaunayModel model)
            {
                return false;
            }

            public override Property Post(TriangulationMesh<IVertex2D> mesh, ConstrainedDelaunayModel model)
            {
                return (true).Trivial(true);
            }

            public override string ToString()
            {
                return "NoOp";
            }
        }

        private class AddConstraint : Command<TriangulationMesh<IVertex2D>, ConstrainedDelaunayModel>
        {
            /// <summary>
            /// The constrained edge we will add to our model
            /// </summary>
            public IEdgeKey EdgeToAdd;

            public AddConstraint(ConstrainedDelaunayModel model)
            {
                EdgeToAdd = model.NextConstraint();
            }

            public override Property Post(TriangulationMesh<IVertex2D> mesh, ConstrainedDelaunayModel model)
            {
                bool edgesIntersect = mesh.AnyMeshEdgesIntersect();
                bool facesCCW = mesh.AreTriangulatedFacesCCW();
                bool facesColinear = mesh.AreTriangulatedFacesColinear();
                bool vertEdges = mesh.Verticies.Count < 3 || mesh.AreTriangulatedVertexEdgesValid();
                bool HasConstrainedEdges = model.AddedConstraintEdges.All(added_edge => mesh.Contains(added_edge));
                bool facesAreTriangles = mesh.AreFacesTriangles();
                bool pass = !edgesIntersect && !facesColinear && facesCCW && vertEdges && HasConstrainedEdges && facesAreTriangles;

                //Trace.WriteLine(pass ? "Pass" : "Fail");

                Property prop = (edgesIntersect == false).Label("Edges intersect")
                        .And(facesCCW.Label("Faces Clockwise"))
                        .And((facesColinear == false).Label("Faces colinear"))
                        .And(vertEdges.Label("Verts with 0 or 1 edges"))
                        .And(facesAreTriangles.Label("Faces aren't triangles"))
                        .And(HasConstrainedEdges.Label(string.Format("Missing Edge Constraint {0}", EdgeToAdd)))
                        .ClassifyMeshSize(mesh.Verticies.Count)
                        .Classify(model.ConstraintEdges.Count == 1, "One constraint")
                        .Classify(model.ConstraintEdges.Count == 0, "No constraints")
                        //.Label(mesh.Verticies.Select(v => v.Position).ToArray().ToJSON().Trim(new char[] { '\n', '\r' }))
                        .Label(mesh.ToString());

                return prop;
            }

            public override bool Pre(ConstrainedDelaunayModel model)
            {
                return base.Pre(model);
            }

            public override TriangulationMesh<IVertex2D> RunActual(TriangulationMesh<IVertex2D> value)
            {
                value.AddConstrainedEdge(new ConstrainedEdge(EdgeToAdd), TriangulatedMeshGenerators.OnProgress);
                if (TriangulatedMeshGenerators.OnProgress != null)
                    TriangulatedMeshGenerators.OnProgress(value);
                return value;
            }

            public override ConstrainedDelaunayModel RunModel(ConstrainedDelaunayModel value)
            {
                //value.NextConstraint();
                return value;
            }

            public override string ToString()
            {
                return string.Format("Add Constraint {0}", EdgeToAdd);
            }
        }
    }
}
