using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AnnotationVizLib
{
    //A bag for the locations and links that describe a structures morphology
    class JSONStructureMorphology
    {
        public ulong StructureID;
        public List<object> Nodes = [];
        public List<object> Edges = [];
        public List<JSONStructureMorphology> Children = [];
    }

    public class MorphologyJSONView
    {
        readonly List<JSONStructureMorphology> StructureMorphologies = [];

        static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            MaxDepth = 64
        };


        static MorphologyJSONView()
        {

        }

        public static MorphologyJSONView ToJSON(MorphologyGraph graph)
        {
            MorphologyJSONView JSONView = new();

            foreach (MorphologyGraph g in graph.Subgraphs.Values)
            {
                JSONView.StructureMorphologies.Add(MorphologyGraphToJSONStructureMorphology(g));
            }

            return JSONView;
        }
        private static JSONStructureMorphology MorphologyGraphToJSONStructureMorphology(MorphologyGraph graph)
        {
            JSONStructureMorphology JSONView = new()
            {
                StructureID = graph.StructureID
            };
            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                JSONView.Nodes.Add(new
                {
                    ID = node.Key,
                    Shape = node.Location.Geometry().STAsText().ToString()
                });
            }

            foreach (MorphologyEdge edge in graph.Edges.Values)
            {
                MorphologyNode SourceNode = graph.Nodes[edge.SourceNodeKey];
                MorphologyNode TargetNode = graph.Nodes[edge.TargetNodeKey];

                JSONView.Edges.Add(new
                {
                    A = SourceNode.Key.ToString(),
                    B = TargetNode.Key.ToString()
                });
            }

            foreach (MorphologyGraph g in graph.Subgraphs.Values)
            {
                JSONView.Children.Add(MorphologyGraphToJSONStructureMorphology(g));
            }

            return JSONView;
        }

        public override string ToString() =>
            // Serialize the object to JSON
            JsonSerializer.Serialize(new { Morphology = this.StructureMorphologies }, jsonOptions);

        public void SaveJSON(string JSONFileFullPath)
        {
            using FileStream fl = new(JSONFileFullPath, FileMode.Create, FileAccess.Write);
            using (StreamWriter write = new(fl))
            {
                write.Write(this.ToString());
                write.Close();
            }
            fl.Close();
        }
    }
}
