
using Newtonsoft.Json.Linq;
using System.IO;

namespace AnnotationVizLib
{
    public class MotifJSONView
    {
        JArray edgesJSON = null;
        JArray nodesJSON = null;

        static MotifJSONView()
        {

        }

        static public MotifJSONView ToJSON(MotifGraph graph)
        {
            int edgeCount = 0;
            MotifJSONView JSONView = new MotifJSONView();

            JSONView.nodesJSON = new JArray();
            JSONView.edgesJSON = new JArray();

            foreach (MotifNode node in graph.Nodes.Values)
            {
                dynamic obj = new JObject();
                obj.Label = node.Key;
                obj.Structures = node.Structures.ToJArray();
                obj.InputCount = node.InputEdgesCount;
                obj.OutputCount = node.OutputEdgesCount;
                obj.BidirectionalCount = node.BidirectionalEdgesCount;

                NewtonsoftJSONExtensions.AddAttributes(obj, node.Attributes);

                JSONView.nodesJSON.Add(obj);
            }

            foreach (MotifEdge edge in graph.Edges.Values)
            {
                if (edge.SourceNodeKey == null || edge.TargetNodeKey == null)
                    continue;

                MotifNode SourceNode = graph.Nodes[edge.SourceNodeKey];
                MotifNode TargetNode = graph.Nodes[edge.TargetNodeKey];
                string KeyString = SourceNode.Key.ToString() + "-" + TargetNode.Key.ToString() + "," + edge.SynapseType;

                dynamic obj = new JObject();
                obj.ID = KeyString;
                obj.SourceNode = SourceNode.Key;
                obj.TargetNode = TargetNode.Key;
                obj.Directional = edge.Directional;
                obj.IsLoop = edge.IsLoop;
                obj.Type = edge.SynapseType;

                obj.Sources = JObject.FromObject(edge.SourceStructIDs);
                obj.Targets = JObject.FromObject(edge.TargetStructIDs);

                NewtonsoftJSONExtensions.AddAttributes(obj, edge.Attributes);

                JSONView.edgesJSON.Add(obj);
                edgeCount++;
            }


            return JSONView;
        }

        public override string ToString()
        {
            dynamic graph = new JObject();
            graph.nodes = this.nodesJSON;
            graph.edges = this.edgesJSON;

            return graph.ToString();
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
