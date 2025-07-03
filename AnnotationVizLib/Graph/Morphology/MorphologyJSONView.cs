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
        public List<object> Nodes = new List<object>();
        public List<object> Edges = new List<object>();
        public List<JSONStructureMorphology> Children = new List<JSONStructureMorphology>();
    }

    public class MorphologyJSONView
    {
        readonly List<JSONStructureMorphology> StructureMorphologies = new List<JSONStructureMorphology>();
        
        static JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            MaxDepth=64
        };
        

        static MorphologyJSONView()
        {

        }

        static public MorphologyJSONView ToJSON(MorphologyGraph graph)
        {
            MorphologyJSONView JSONView = new MorphologyJSONView();

            foreach (MorphologyGraph g in graph.Subgraphs.Values)
            {
                JSONView.StructureMorphologies.Add(MorphologyGraphToJSONStructureMorphology(g));
            }

            return JSONView;
        }
        static private JSONStructureMorphology MorphologyGraphToJSONStructureMorphology(MorphologyGraph graph)
        {
            JSONStructureMorphology JSONView = new JSONStructureMorphology
            {
                StructureID = graph.StructureID
            };
            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                JSONView.Nodes.Add(new
                {
                    ID = node.Key,
                    Shape = node.Location.Geometry.STAsText().ToString()
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

        public override string ToString()
        {
            // Serialize the object to JSON
            return JsonSerializer.Serialize(new { Morphology = this.StructureMorphologies }, jsonOptions);
        }

        public void SaveJSON(string JSONFileFullPath)
        {
            using (FileStream fl = new FileStream(JSONFileFullPath, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter write = new StreamWriter(fl))
                {
                    write.Write(this.ToString());
                    write.Close();
                }
                fl.Close();
            }
        }
    }
}
