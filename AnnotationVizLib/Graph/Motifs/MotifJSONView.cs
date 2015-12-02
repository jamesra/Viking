using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Web;
using System.Web.Script.Serialization;

namespace AnnotationVizLib 
{
    public class MotifJSONView
    {
        List<object> edgesJSON = null;

        static MotifJSONView()
        {

        }

        static public MotifJSONView ToJSON(MotifGraph graph)
        {
            int edgeCount = 0;
            MotifJSONView JSONView = new MotifJSONView();

            JSONView.edgesJSON = new List<object>(graph.Edges.Count);

            foreach (MotifEdge edge in graph.Edges.Values)
            {
                if (edge.SourceNodeKey == null || edge.TargetNodeKey == null)
                    continue; 

                MotifNode SourceNode = graph.Nodes[edge.SourceNodeKey];
                MotifNode TargetNode = graph.Nodes[edge.TargetNodeKey];
                string KeyString = SourceNode.Key.ToString() + "-" + TargetNode.Key.ToString() + "," + edge.SynapseType;

                JSONView.edgesJSON.Add(new
                {
                    id = edgeCount,
                    source = SourceNode.Key.ToString(),
                    target = TargetNode.Key.ToString(), 
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
                oSerializer.MaxJsonLength = 268435456;
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
