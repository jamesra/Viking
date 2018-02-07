using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simple.OData.Client;
using AnnotationVizLib.SimpleOData;
using System.Diagnostics;

namespace AnnotationVizLib.SimpleODataClient
{
    public class SimpleODataSpatialDataFactory
    {
        public static void AppendSpatialDataFromOData(NeuronGraph graph, Uri Endpoint)
        {
            ODataClientSettings s = new ODataClientSettings();
            Simple.OData.Client.ODataClient client = new Simple.OData.Client.ODataClient(Endpoint);
            var scale = client.GetScale();
            /*
            System.Threading.Tasks.Parallel.ForEach(graph.Nodes.Values, node =>
            {
                Simple.OData.Client.ODataClient taskclient = new Simple.OData.Client.ODataClient(Endpoint);
                AppendAreaToConnections(node, taskclient);
                AppendNeuronSpatialData(node, taskclient);
            });
            */
            foreach (NeuronNode node in graph.Nodes.Values)
            { 
                AppendAreaToConnections(node, client);
                AppendNeuronSpatialData(node, client);
            } 
            
        }

        public static void AppendNeuronSpatialData(NeuronNode node, ODataClient client)
        {
            var annotations = new ODataFeedAnnotations();
            string queryString = string.Format("StructureSpatialView({0})", node.Key);
            Task<IDictionary<string, object>> taskStructureDicts = client.FindEntryAsync(queryString);
            Debug.Assert(taskStructureDicts != null);
            taskStructureDicts.Wait();
            IDictionary<string, object> StructuresDicts = taskStructureDicts.Result;

            AppendDictionaryToAttributes(node, StructuresDicts);
        }

        private static void AppendDictionaryToAttributes(NeuronNode node, IDictionary<string, object> data)
        {
            foreach(string key in data.Keys)
            {
                if (key == "ID" ||
                    key == "ParentID" ||
                    key == "TypeID")
                    continue;

                node.Attributes[key] = data[key];
            }
        }


        public static void AppendAreaToConnections(NeuronNode node, ODataClient client)
        {
            var annotations = new ODataFeedAnnotations();
            string queryString = string.Format("StructureSpatialView?$filter=ParentID eq {0}&$select=ID,Area", node.Key);
            Task<IEnumerable<IDictionary<string, object>>> taskStructureDicts = client.FindEntriesAsync(queryString, annotations);
            Debug.Assert(taskStructureDicts != null);
            taskStructureDicts.Wait();
            IEnumerable<IDictionary<string, object>> StructuresDicts = taskStructureDicts.Result;

            AppendNeuronEdgeArea(node, StructuresDicts);

            while (annotations.NextPageLink != null)
            {
                taskStructureDicts = client.FindEntriesAsync(annotations.NextPageLink.ToString(), annotations);
                taskStructureDicts.Wait();
                StructuresDicts = taskStructureDicts.Result;

                AppendNeuronEdgeArea(node, StructuresDicts);
            }
        }

        private static void AppendNeuronEdgeArea(NeuronNode node, IEnumerable<IDictionary<string, object>> nodeData)
        {
            foreach (IDictionary<string, object> dict in nodeData)
            {
                Debug.Assert(dict.ContainsKey("ID"));
                if (!dict.ContainsKey("ID"))
                    continue;

                ulong EdgeStructureID = System.Convert.ToUInt64(dict["ID"]);

                Debug.Assert(dict.ContainsKey("Area"));
                if (!dict.ContainsKey("Area"))
                {
                    continue;
                }

                double Area = (double)dict["Area"];

                foreach (string key in dict.Keys)
                {
                    foreach(SortedSet<NeuronEdge> edges in node.Edges.Values)
                    {
                        foreach (NeuronEdge edge in edges)
                        {
                            if (edge.Links.Any(e => e.SourceID == EdgeStructureID))
                            {
                                if (!edge.Attributes.ContainsKey("TotalSourceArea"))
                                    edge.Attributes.Add("TotalSourceArea", 0);

                                edge.Attributes["TotalSourceArea"] = System.Convert.ToDouble(edge.Attributes["TotalSourceArea"]) + Area;
                            }

                            if (edge.Links.Any(e => e.TargetID == EdgeStructureID))
                            {
                                if (!edge.Attributes.ContainsKey("TotalTargetArea"))
                                    edge.Attributes.Add("TotalTargetArea", 0);

                                edge.Attributes["TotalTargetArea"] = System.Convert.ToDouble(edge.Attributes["TotalTargetArea"]) + Area;
                            }
                        }
                    }
                }
            }
        }
    }
}
