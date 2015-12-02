using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Web;
using System.Web.Script.Serialization;

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
        List<JSONStructureMorphology> StructureMorphologies = new List<JSONStructureMorphology>();

        static MorphologyJSONView()
        {

        }

        static public MorphologyJSONView ToJSON(MorphologyGraph graph)
        {
            int edgeCount = 0;
            MorphologyJSONView JSONView = new MorphologyJSONView();

            foreach(MorphologyGraph g in graph.Subgraphs.Values)
            {
                JSONView.StructureMorphologies.Add(MorphologyGraphToJSONStructureMorphology(g));
            }

            return JSONView;
        }
        static private JSONStructureMorphology MorphologyGraphToJSONStructureMorphology(MorphologyGraph graph)
        {
            JSONStructureMorphology JSONView = new JSONStructureMorphology();
            JSONView.StructureID = graph.ID;
            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                JSONView.Nodes.Add(new
                {
                    ID = node.Key,
                    Shape = node.Location.VolumeShape.Geometry.WellKnownText
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
            StringBuilder sb = new StringBuilder();

            using (StringWriter fs = new StringWriter(sb))
            {
                System.Web.Script.Serialization.JavaScriptSerializer oSerializer = new JavaScriptSerializer();
                oSerializer.MaxJsonLength = 268435456;
                fs.Write(oSerializer.Serialize(new { Morphology = this.StructureMorphologies }));
                fs.Close();
            }

            return sb.ToString();
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
