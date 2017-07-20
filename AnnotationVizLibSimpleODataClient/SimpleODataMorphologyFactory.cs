using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Simple.OData.Client.V4;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using Geometry;
using AnnotationVizLib.SimpleOData;

namespace AnnotationVizLib.SimpleOData
{  
    public static class SimpleODataMorphologyFactory
    {
        public static MorphologyGraph FromOData(ICollection<long> StructureIDs, bool include_children, Uri Endpoint)
        {
            var client = new Simple.OData.Client.ODataClient(Endpoint);

            var scale = client.GetScale();
            Debug.Assert(scale != null);

            MorphologyGraph rootGraph = new MorphologyGraph(0, scale); 
            if (StructureIDs == null)
            {
                //TODO: Retrieve the full network if no structureID's are passed
                return rootGraph;
            }

            List<Structure> listStructures = LoadStructures(client, StructureIDs, rootGraph.scale);
            MorphologyForStructures(Endpoint, rootGraph, listStructures, include_children, rootGraph.scale);
            
            return rootGraph;
        }

        public static MorphologyGraph FromODataLocationIDs(ICollection<long> LocationIDs, Uri Endpoint)
        {
            var client = new Simple.OData.Client.ODataClient(Endpoint);

            var scale = client.GetScale();
            Debug.Assert(scale != null);

            MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
            if (LocationIDs == null)
            {
                //TODO: Retrieve the full network if no structureID's are passed
                return rootGraph;
            }

            List<Task<Location>> listTasks = new List<Task<SimpleOData.Location>>();

            foreach (long ID in LocationIDs)
            {
                Task<Location> t = client.For<Location>().Filter(l => (long)l.ID == ID).FindEntryAsync();

                listTasks.Add(t);
            }

            long StructureID = 0;

            List<Location> listLocations = new List<Location>();
            foreach (Task<Location> t in listTasks)
            {
                t.Wait();
                Location l = t.Result;
                if (l != null)
                {
                    l.scale = scale;

                    listLocations.Add(l);

                    StructureID = (long)l.ParentID;
                }
            }

            //Get a structure
            Task<Structure> st = client.For<Structure>().Filter(s => (long)s.ID == StructureID).FindEntryAsync();
            st.Wait();


            Structure Parent = st.Result;

            MorphologyGraph graph = new MorphologyGraph((ulong)Parent.ID, scale, Parent);

            LoadStructureLocationLinks(client, new Structure[] { Parent });

            foreach (Location loc in listLocations)
            {
                //TODO: REMOVE Z * 10
                //loc.Z *= 10;
                graph.AddNode(new MorphologyNode((ulong)loc.ID, loc, graph));
            }

            AddLocationEdges(graph, Parent.LocationLinks.ToArray());

            return graph;
        }

        private static List<Structure> LoadStructures(Simple.OData.Client.ODataClient client, ICollection<long> StructureIDs, Geometry.Scale scale)
        {
            List<Task<Structure>> listTasks = new List<Task<SimpleOData.Structure>>();
            List<Structure> listStructures = new List<Structure>();

            foreach (long ID in StructureIDs)
            {
                Task<Structure> t = client.For<Structure>().Filter(s => s.ID == (ulong)ID)
                                                           .Expand(s => s.Type)
                                                           //.Expand(s => s.Locations.Select(l => new Location {ID = l.ID, ParentID = l.ParentID, VolumeShape = l.VolumeShape, Z = l.Z, Tags = l.Tags, Terminal = l.Terminal, OffEdge = l.OffEdge}))
                                                           .Expand(s => s.Locations)
                                                           .Expand(s => s.Children)
                                                           .Expand(s => s.SourceOfLinks)
                                                           .Expand(s => s.TargetOfLinks).FindEntryAsync();
                listTasks.Add(t);
            }

            foreach(Task<Structure> t in listTasks)
            {
                t.Wait();
                Structure s = t.Result;
                if(s != null)
                {
                    foreach (Location l in s.Locations)
                    {
                        l.scale = scale;
                    }

                    listStructures.Add(s);
                }
            }

            LoadStructureLocationLinks(client, listStructures);

            return listStructures;
        }
        
        
        private static void LoadStructureLocationLinks(Simple.OData.Client.ODataClient client, ICollection<Structure> structures)
        {
            SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> tasks = new SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>>();
            foreach (Structure s in structures)
            {
                if (s.LocationLinks == null)
                {
                    Task <IEnumerable<IDictionary< string, object>>> t = client.FindEntriesAsync(string.Format("StructureLocationLinks(StructureID={0})?$select=A,B", s.ID));
                    tasks[s.ID] = t;
                }
            }

            foreach (Structure s in structures)
            {
                if (!tasks.ContainsKey(s.ID))
                {
                    continue;
                }

                Task<IEnumerable<IDictionary<string, object>>> T = tasks[s.ID];
                T.Wait();
                if (!T.IsFaulted)
                {
                    s.LocationLinks = T.Result.Select(dict => LocationLink.FromDictionary(dict)).ToArray();
                }
            }

            /*
            //A bug, https://github.com/object/Simple.OData.Client/issues/340, prevents this from working correctly.  When it is fixed these lines can call the function.

            SortedList<ulong, Task<LocationLink[]>> tasks = new SortedList<ulong, Task<LocationLink[]>>();
            foreach(Structure s in structures)
            {
                if (s.LocationLinks == null)
                {
                    
                    //Task<LocationLink[]> t = client.ExecuteFunctionAsArrayAsync<LocationLink>("StructureLocationLinks", new Dictionary<string, object>() { { "StructureID", System.Convert.ToInt64(s.ID) } });
                    tasks[s.ID] = t;
                }
            }

            foreach(Structure s in structures)
            {
                if (!tasks.ContainsKey(s.ID))
                {
                    continue;
                }

                Task<LocationLink[]> T = tasks[s.ID];
                T.Wait();
                if(!T.IsFaulted)
                {
                    s.LocationLinks = T.Result;
                }
            }
            */
        }

        /// <summary>
        /// Add the morphology for the passed structure ID to the provided root graph
        /// </summary>
        /// <param name="rootGraph"></param>
        /// <param name="StructureIDs"></param>
        private static async void MorphologyForStructures(Uri Endpoint, MorphologyGraph rootGraph, ICollection<Structure> Structures, bool include_children, Geometry.Scale scale)
        {
            //Queries.PopulateStructureTypes();

            // Get the nodes and build graph for numHops            
//            System.Threading.Tasks.Parallel.ForEach<Structure>(Structures, s =>

            foreach (Structure s in Structures)
            {
                MorphologyGraph graph = MorphologyForStructure(s, scale);
                if (graph == null)
                    return;

                rootGraph.AddSubgraph(graph);

                if (include_children && s.Children != null && s.Children.Any())
                {
                    //Optimization, use the already loaded StructureTypes instead of expand
                    //MorphologyGraph subgraph = await FromOData(s.Children.Select(child => System.Convert.ToInt64(child.ID)).ToList(), include_children, Endpoint);
                    //graph.AddSubgraph(subgraph);

                    List<Structure> child_structs = LoadStructures(new Simple.OData.Client.ODataClient(Endpoint), s.Children.Select(child => System.Convert.ToInt64(child.ID)).ToArray(), scale);
                    MorphologyForStructures(Endpoint, graph, child_structs, include_children, scale);

                    //IList<Structure> child_structs = client.Structures.Expand(st => st.Locations).Expand(st => st.Type).Expand(st => st.Children).Where(st => st.ParentID == s.ID).ToList();
                    //LoadStructureLocationLinks(container, child_structs);
                    //MorphologyForStructures(container, graph, child_structs, include_children, scale);
                }
            }
            //);
        }

        private static MorphologyGraph MorphologyForStructure(Structure s, Geometry.Scale scale)
        {
            Location[] locations = s.Locations.ToArray();
            LocationLink[] location_links = s.LocationLinks.ToArray();

            if (locations.Length <= 0)
            {
                return null;
            }

            MorphologyGraph graph = new MorphologyGraph((ulong)s.ID, scale, s);

            foreach (Location loc in locations)
            {
                //TODO: REMOVE Z * 10
             //   loc.Z *= 10;
                graph.AddNode(new MorphologyNode((ulong)loc.ID, loc, graph));
            }

            AddLocationEdges(graph, location_links);

            return graph;
        }

        private static void AddLocationEdges(MorphologyGraph graph, LocationLink[] location_links)
        {
            if (location_links == null)
                return;

            foreach (LocationLink loc_link in location_links)
            {
                if (graph.Nodes.ContainsKey(loc_link.A) && graph.Nodes.ContainsKey(loc_link.B))
                {
                    //Only add the links with ID's less than ours to prevent duplicate links in the graph
                    graph.AddEdge(new MorphologyEdge(graph, loc_link.A, loc_link.B));
                }
            }

            return;
        }
        
    }
}
