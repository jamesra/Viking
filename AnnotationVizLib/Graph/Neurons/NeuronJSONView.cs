using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;

namespace AnnotationVizLib
{
    public class NeuronJSONView
    {
        List<object> edgesJSON = null;
        List<object> nodesJSON = null;

        static NeuronJSONView()
        {

        }
         
        static public NeuronJSONView ToJSON(NeuronGraph graph)
        {
            
            int edgeCount = 0;
            NeuronJSONView JSONView = new NeuronJSONView();

            JSONView.nodesJSON = new List<object>(graph.Nodes.Count);
            JSONView.edgesJSON = new List<object>(graph.Edges.Count);

            foreach (NeuronNode node in graph.Nodes.Values)
            {
                JSONView.nodesJSON.Add(new
                {
                    StructureID = node.Key,
                    TypeID = node.Structure.TypeID,
                    Label = node.Structure.Label,
                    Tags = node.Structure.TagsXML
                });
            }

            foreach (NeuronEdge edge in graph.Edges.Values)
            {
                if (!graph.Nodes.ContainsKey(edge.SourceNodeKey) || !graph.Nodes.ContainsKey(edge.TargetNodeKey))
                    continue; 
                 
                NeuronNode SourceNode = graph.Nodes[edge.SourceNodeKey];
                NeuronNode TargetNode = graph.Nodes[edge.TargetNodeKey];
                string KeyString = SourceNode.Structure.ID.ToString() + "-" + TargetNode.Structure.ID.ToString() + " via " + edge.SynapseType + " from " + edge.PrintChildLinks() ;

                JSONView.edgesJSON.Add(new
                {
                    ID = edgeCount,
                    SourceStructureID = SourceNode.Key,
                    TargetStructureID = TargetNode.Key,
                    Label = KeyString,
                    Type = edge.SynapseType,
                    Directional = edge.Directional,
                    Links = AddEdgeLinks(edge)
                });

                edgeCount++; 
            }
             
            return JSONView;
        }

        private static List<object> AddEdgeLinks(NeuronEdge edge)
        {
            List<object> listLinks = new List<object>();
            foreach(var link in edge.Links)
            {
                listLinks.Add(new
                {
                    SourceID = link.SourceID,
                    TargetID = link.TargetID,
                    Directional = link.Directional
                });
            }

            return listLinks;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            using (StringWriter fs = new StringWriter(sb))
            {
                System.Web.Script.Serialization.JavaScriptSerializer oSerializer = new JavaScriptSerializer();
                oSerializer.MaxJsonLength = 268435456;
                fs.Write(oSerializer.Serialize(new { nodes = this.nodesJSON, edges = this.edgesJSON }));
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
