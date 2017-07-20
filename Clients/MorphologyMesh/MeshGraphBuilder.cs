using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using AnnotationVizLib;

namespace MorphologyMesh
{
    interface IMeshGenerator
    {
        MeshNode CreateNode(MorphologyNode node);

        MeshEdge CreateEdge(MorphologyEdge edge);

        /// <summary>
        /// Create the connection port for the verticies we generated for the node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        ConnectionVerticies CreatePort(MorphologyNode source);

        //DynamicRenderMesh<T> CreateMesh(MorphologyNode node);
    }

    static class MeshGraphBuilder
    {
        /// <summary>
        /// Convert a morphology graph to an unprocessed mesh graph
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static MeshGraph ConvertToMeshGraph(this MorphologyGraph graph)
        {
            MeshGraph meshGraph = new MeshGraph();

            meshGraph.SectionThickness = graph.SectionThickness;

            //Create a graph where each node is a set of verticies.
            ConcurrentBag<MeshNode> nodes = new ConcurrentBag<MeshNode>();

#if !DEBUG
            graph.Nodes.Values.AsParallel().ForAll(node =>
            {
                MeshNode newNode = SmoothMeshGraphGenerator.CreateNode(node);
                newNode.MeshGraph = meshGraph;
                nodes.Add(newNode);
            });
#else
            
            foreach(var node in graph.Nodes.Values)
            {
                MeshNode newNode = SmoothMeshGraphGenerator.CreateNode(node);
                newNode.MeshGraph = meshGraph;
                nodes.Add(newNode);
            }

#endif

            MeshNode meshNode = null;
            while (nodes.TryTake(out meshNode))
            {
                meshGraph.AddNode(meshNode);
            }

            foreach (MorphologyEdge edge in graph.Edges.Values)
            {
                MeshEdge mEdge = SmoothMeshGraphGenerator.CreateEdge(graph.Nodes[edge.SourceNodeKey], graph.Nodes[edge.TargetNodeKey]);
                meshGraph.AddEdge(mEdge);
            }

            //Create multiple ports for branches
        /*    foreach(MeshNode node in meshGraph.Nodes.Values.Where(n => n.GetEdgesAbove().Length > 1).ToArray())
            {
                CreatePortsForBranch(node, node.GetEdgesAbove().SelectMany(e => node.Edges[e]).ToArray() );
            }*/
             
            return meshGraph;
        }

        private static void CreatePortsForBranch(MeshNode node, MeshEdge[] edges)
        {
            MeshGraph graph = node.MeshGraph;

            //OK, Voronoi diagram the shapes.  Create new ports.
            MeshNode[] other_nodes;
            other_nodes = edges.Select(e => graph.Nodes[e.SourceNodeKey == node.Key ? e.TargetNodeKey : e.SourceNodeKey]).ToArray();


        }
    }
}
