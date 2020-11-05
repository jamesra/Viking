using AnnotationVizLib.SimpleOData;
using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AnnotationVizLib.SimpleODataClient
{
    public class SimpleODataSpatialDataFactory
    {

        public static void AppendSpatialDataFromOData(NeuronGraph graph, Uri Endpoint, ICollection<long> IDs, uint Hops)
        {
            ODataClientSettings s = new ODataClientSettings();
            Simple.OData.Client.ODataClient client = new Simple.OData.Client.ODataClient(Endpoint);
            AppendAreaToConnections(graph, client, IDs, Hops);
            AppendNeuronSpatialData(graph, client, IDs, Hops);
        }

        public static void AppendNeuronSpatialData(NeuronGraph graph, ODataClient client, ICollection<long> IDs, uint Hops)
        {
            var annotations = new ODataFeedAnnotations();

            string queryString = null;
            if ((IDs == null || IDs.Count == 0) && graph.Nodes.Count > 0)
            {
                //If no specific ID requested and there are nodes in the graph then request everything
                queryString = string.Format("NetworkSpatialData", IDs.ToODataArrayParameterString(), Hops);
            }
            else
            {
                queryString = string.Format("NetworkSpatialData(IDs={0},Hops={1})", IDs.ToODataArrayParameterString(), Hops);
            }

            Task<IEnumerable<IDictionary<string, object>>> taskStructureDicts = client.FindEntriesAsync(queryString, annotations);
            Debug.Assert(taskStructureDicts != null);
            taskStructureDicts.Wait();

            IEnumerable<IDictionary<string, object>> StructureDicts = null;

            while (annotations.NextPageLink != null)
            {
                StructureDicts = taskStructureDicts.Result;
                taskStructureDicts = client.FindEntriesAsync(annotations.NextPageLink.ToString(), annotations);

                AppendDictionaryToAttributes(graph, StructureDicts);
                taskStructureDicts.Wait();
            }

            StructureDicts = taskStructureDicts.Result;
            AppendDictionaryToAttributes(graph, StructureDicts);
        }

        public static void AppendNeuronSpatialData(NeuronNode node, ODataClient client)
        {
            var annotations = new ODataFeedAnnotations();
            string queryString = string.Format("StructureSpatialCaches({0})", node.Key);
            Task<IDictionary<string, object>> taskStructureDicts = client.FindEntryAsync(queryString);
            Debug.Assert(taskStructureDicts != null);
            taskStructureDicts.Wait();
            IDictionary<string, object> StructuresDicts = taskStructureDicts.Result;

            AppendDictionaryToAttributes(node, StructuresDicts);
        }

        private static void AppendDictionaryToAttributes(NeuronGraph graph, IEnumerable<IDictionary<string, object>> StructureDicts)
        {
            foreach (IDictionary<string, object> dict in StructureDicts)
            {
                if (!dict.ContainsKey("ID"))
                {
                    continue;
                }

                long ID = System.Convert.ToInt64(dict["ID"]);

                if (graph.Nodes.ContainsKey(ID))
                {
                    AppendDictionaryToAttributes(graph.Nodes[ID], dict);
                }
            }
        }

        private static void AppendDictionaryToAttributes(NeuronNode node, IDictionary<string, object> data)
        {
            foreach (string key in data.Keys)
            {
                if (key == "ID" ||
                    key == "ParentID" ||
                    key == "TypeID")
                    continue;

                node.Attributes[key] = data[key];
            }
        }

        private static Dictionary<ulong, long> BuildChildToParentMap(NeuronGraph graph)
        {
            Dictionary<ulong, long> ChildToParent = new Dictionary<ulong, long>();
            foreach (NeuronNode node in graph.Nodes.Values)
            {
                foreach (ulong childID in node.EdgeSourceChildStructureIDs)
                {
                    if (!ChildToParent.ContainsKey(childID))
                        ChildToParent.Add(childID, node.Key);
                }

                foreach (ulong childID in node.EdgeTargetChildStructureIDs)
                {
                    if (!ChildToParent.ContainsKey(childID))
                        ChildToParent.Add(childID, node.Key);
                }
            }

            return ChildToParent;
        }


        private static Dictionary<ulong, SortedSet<NeuronEdge>> BuildChildToEdgeMap(NeuronGraph graph)
        {
            Dictionary<ulong, SortedSet<NeuronEdge>> IDToEdge = new Dictionary<ulong, SortedSet<NeuronEdge>>();
            foreach (NeuronEdge e in graph.Edges.Values)
            {
                foreach (ulong SourceID in e.SourceIDs)
                {
                    AddEdge(IDToEdge, SourceID, e);
                }

                foreach (ulong TargetID in e.TargetIDs)
                {
                    AddEdge(IDToEdge, TargetID, e);
                }
            }

            return IDToEdge;
        }

        private static void AddEdge(Dictionary<ulong, SortedSet<NeuronEdge>> dict, ulong ChildID, NeuronEdge value)
        {
            if (!dict.ContainsKey(ChildID))
            {
                dict.Add(ChildID, new SortedSet<NeuronEdge>());
            }

            dict[ChildID].Add(value);
        }

        public static void AppendAreaToConnections(NeuronGraph graph, ODataClient client, ICollection<long> IDs, uint Hops)
        {
            var annotations = new ODataFeedAnnotations();

            string queryString = null;
            if ((IDs == null || IDs.Count == 0) && graph.Nodes.Count > 0)
            {
                //If no specific ID requested and there are nodes in the graph then request everything
                queryString = string.Format("NetworkEdgeSpatialData", IDs.ToODataArrayParameterString(), Hops);
            }
            else
            {
                queryString = string.Format("NetworkEdgeSpatialData(IDs={0},Hops={1})", IDs.ToODataArrayParameterString(), Hops);
            }

            Task<IEnumerable<IDictionary<string, object>>> taskStructureDicts = client.FindEntriesAsync(queryString, annotations);
            Debug.Assert(taskStructureDicts != null);

            Dictionary<ulong, SortedSet<NeuronEdge>> IDToEdge = BuildChildToEdgeMap(graph);
            Dictionary<ulong, long> ChildToParent = BuildChildToParentMap(graph);

            taskStructureDicts.Wait();

            IEnumerable<IDictionary<string, object>> StructureEdgeDicts = null;
            while (annotations.NextPageLink != null)
            {
                StructureEdgeDicts = taskStructureDicts.Result;
                taskStructureDicts = client.FindEntriesAsync(annotations.NextPageLink.ToString(), annotations);
                AddSpatialDataToEdges(graph, StructureEdgeDicts, IDToEdge);
                taskStructureDicts.Wait();
            }

            StructureEdgeDicts = taskStructureDicts.Result;
            AddSpatialDataToEdges(graph, StructureEdgeDicts, IDToEdge);
        }

        public static void AddSpatialDataToEdges(NeuronGraph graph, IEnumerable<IDictionary<string, object>> EdgeStructureDicts, Dictionary<ulong, SortedSet<NeuronEdge>> IDToEdge)
        {
            foreach (IDictionary<string, object> childData in EdgeStructureDicts)
            {
                AddAreaToEdges(graph, childData, IDToEdge);
                AddZToEdges(graph, childData, IDToEdge);
            }
        }

        public static void AddAreaToEdges(NeuronGraph graph, IDictionary<string, object> childData, Dictionary<ulong, SortedSet<NeuronEdge>> IDToEdge)
        {
            Debug.Assert(childData.ContainsKey("ID"));
            if (!childData.ContainsKey("ID"))
                return;

            Debug.Assert(childData.ContainsKey("Area"));
            if (!childData.ContainsKey("Area"))
            {
                return;
            }

            ulong ChildKey = System.Convert.ToUInt64(childData["ID"]);
            double Area = System.Convert.ToDouble(childData["Area"]);

            if (!IDToEdge.ContainsKey(ChildKey))
                return;

            SortedSet<NeuronEdge> edges = IDToEdge[ChildKey];

            foreach (NeuronEdge edge in edges.Where(e => e.SourceIDs.Contains(ChildKey)))
            {
                edge.TotalSourceArea += Area;
            }

            foreach (NeuronEdge edge in edges.Where(e => e.TargetIDs.Contains(ChildKey)))
            {
                edge.TotalTargetArea += Area;
            }
        }

        public static void AddZToEdges(NeuronGraph graph, IDictionary<string, object> childData, Dictionary<ulong, SortedSet<NeuronEdge>> IDToEdge)
        {
            Debug.Assert(childData.ContainsKey("ID"));
            if (!childData.ContainsKey("ID"))
                return;

            Debug.Assert(childData.ContainsKey("MinZ"));
            if (!childData.ContainsKey("MinZ"))
            {
                return;
            }

            Debug.Assert(childData.ContainsKey("MaxZ"));
            if (!childData.ContainsKey("MaxZ"))
            {
                return;
            }

            ulong ChildKey = System.Convert.ToUInt64(childData["ID"]);
            double MinZ = System.Convert.ToDouble(childData["MinZ"]);
            double MaxZ = System.Convert.ToDouble(childData["MaxZ"]);

            if (!IDToEdge.ContainsKey(ChildKey))
                return;

            SortedSet<NeuronEdge> edges = IDToEdge[ChildKey];

            foreach (NeuronEdge edge in edges.Where(e => e.SourceIDs.Contains(ChildKey)))
            {
                if (edge.MinZ > MinZ)
                    edge.MinZ = MinZ;

                if (edge.MaxZ < MaxZ)
                    edge.MaxZ = MaxZ;
            }
        }
    }
}
