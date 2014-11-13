using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;

namespace AnnotationUtils
{
    public class NeuronJSONView
    {
        List<object> edgesJSON = null; 

        static NeuronJSONView()
        {

        }
         
        static public NeuronJSONView ToJSON(NeuronGraph graph)
        {
            
            int edgeCount = 0;
            NeuronJSONView JSONView = new NeuronJSONView();

            JSONView.edgesJSON = new List<object>(graph.Edges.Count);

            foreach (NeuronEdge edge in graph.Edges.Values)
            {
                if (!graph.Nodes.ContainsKey(edge.SourceNodeKey) || !graph.Nodes.ContainsKey(edge.TargetNodeKey))
                    continue; 

                NeuronNode SourceNode = graph.Nodes[edge.SourceNodeKey];
                NeuronNode TargetNode = graph.Nodes[edge.TargetNodeKey];
                string KeyString = SourceNode.Structure.ID.ToString() + "-" + TargetNode.Structure.ID.ToString() + " via " + edge.SynapseType + " from " + edge.PrintChildLinks() ;

                JSONView.edgesJSON.Add(new
                {
                    id = edgeCount,
                    node1 = SourceNode.Key,
                    node2 = TargetNode.Key,
                    label = KeyString,
                    type = edge.SynapseType
                });

                edgeCount++; 
            }
             

            return JSONView;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            using (StringWriter fs = new StringWriter(sb))
            {
                System.Web.Script.Serialization.JavaScriptSerializer oSerializer = new JavaScriptSerializer();
                fs.Write(oSerializer.Serialize(new { page = "1", total = (this.edgesJSON.Count % 10 + 1), records = this.edgesJSON.Count.ToString(), rows = this.edgesJSON }));
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
