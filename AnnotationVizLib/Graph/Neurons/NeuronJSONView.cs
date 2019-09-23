using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using System.IO;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Web.Script.Serialization;

namespace AnnotationVizLib
{
    public class NeuronJSONView
    {
        JArray edgesJSON = null;
        JArray nodesJSON = null;

        static NeuronJSONView()
        {

        }
         
        static public NeuronJSONView ToJSON(NeuronGraph graph)
        {
            int edgeCount = 0;
            NeuronJSONView JSONView = new NeuronJSONView(); 

            JSONView.nodesJSON = new JArray();
            JSONView.edgesJSON = new JArray();
            
            foreach (NeuronNode node in graph.Nodes.Values)
            {
                dynamic obj = new JObject();
                obj.StructureID = node.Key;
                obj.TypeID = node.Structure.TypeID;
                obj.Label = node.Structure.Label;
                obj.Tags = node.Structure.TagsXML;

                NewtonsoftJSONExtensions.AddAttributes(obj, node.Attributes);

                JSONView.nodesJSON.Add(obj);
            }

            foreach (NeuronEdge edge in graph.Edges.Values)
            {
                if (!graph.Nodes.ContainsKey(edge.SourceNodeKey) || !graph.Nodes.ContainsKey(edge.TargetNodeKey))
                    continue; 
                 
                NeuronNode SourceNode = graph.Nodes[edge.SourceNodeKey];
                NeuronNode TargetNode = graph.Nodes[edge.TargetNodeKey];
                string KeyString = SourceNode.Structure.ID.ToString() + "-" + TargetNode.Structure.ID.ToString() + " via " + edge.SynapseType + " from " + edge.PrintChildLinks() ;

                dynamic obj = new JObject();
                obj.ID = edgeCount;
                obj.SourceStructureID = SourceNode.Key;
                obj.TargetStructureID = TargetNode.Key;
                obj.Label = KeyString;
                obj.Type = edge.SynapseType;
                obj.Directional = edge.Directional;
                obj.TotalSourceArea = edge.TotalSourceArea;
                obj.TotalTargetArea = edge.TotalTargetArea;
                obj.MinZ = edge.MinZ;
                obj.MaxZ = edge.MaxZ;
                obj.IsLoop = edge.IsLoop;
                obj.Links = AddEdgeLinks(edge);

                NewtonsoftJSONExtensions.AddAttributes(obj, edge.Attributes);

                JSONView.edgesJSON.Add(obj);
                edgeCount++; 
            }
             
            return JSONView;
        }

        private static JArray AddEdgeLinks(NeuronEdge edge)
        {

            dynamic listLinks = new JArray();
            foreach(var link in edge.Links)
            {
                dynamic obj = new JObject();

                obj.SourceID = link.SourceID;
                obj.TargetID = link.TargetID;
                obj.Directional = link.Directional;

                listLinks.Add(obj);
            }

            return listLinks;
        }

        private static void AddAttributes(JObject obj, IDictionary<string, object> attribs)
        {
            foreach(string key in attribs.Keys)
            {
                object value = attribs[key];
                JToken token;
                if(value as JToken != null)
                {
                    token = (JToken)value;
                }
                else
                {
                    token = JToken.FromObject(value);
                }

                obj[key] = token;
            }
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
