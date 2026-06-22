using Geometry;
using Simple.OData.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationVizLib.SimpleOData
{
    public static class SimpleODataMorphologyFactory
    {
        public static async Task<MorphologyGraph> FromODataByTypeIDsAsync(ICollection<ulong> StructureIDs, Uri Endpoint, bool include_children = false)
        {
            ODataClient client = new(Endpoint);
            var scale = client.GetScale();
            Debug.Assert(scale != null);

            MorphologyGraph rootGraph = new(0, scale);

            List<Task<List<Structure>>> tasks = [];
            foreach (var sid in StructureIDs)
            {
                var t = LoadStructuresOfType(client, (long)sid, scale);
                tasks.Add(t);
            }

            List<List<Structure>> results = [.. await Task.WhenAll(tasks)];
            List<Structure> structures = [.. results.SelectMany(x => x)];

            await MorphologyForStructures(Endpoint, rootGraph, structures, include_children, scale);
            return rootGraph;
        }

        /// <summary>
        /// Retrieve the morphology graph for all structures
        /// </summary>
        /// <param name="Endpoint"></param>
        /// <returns></returns>
        public static async Task<MorphologyGraph> FromOData(Uri Endpoint, bool include_children)
        {
            ODataClient client = new(Endpoint);

            var scale = client.GetScale();
            Debug.Assert(scale != null);

            MorphologyGraph rootGraph = new(0, scale);

            List<Structure> listStructures = await LoadRootStructures(client, rootGraph.scale);
            await MorphologyForStructures(Endpoint, rootGraph, listStructures, include_children, rootGraph.scale);

            return rootGraph;
        }

        public static MorphologyGraph FromOData(ICollection<ulong> StructureIDs, bool include_children, Uri Endpoint)
        {
            return FromODataAsync(StructureIDs, include_children, Endpoint).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async overload: retrieve the morphology graph for the given structure IDs.
        /// </summary>
        public static async Task<MorphologyGraph> FromODataAsync(ICollection<ulong> StructureIDs, bool include_children, Uri Endpoint)
        {
            ODataClient client = new(Endpoint);

            var scale = client.GetScale();
            Debug.Assert(scale != null);

            MorphologyGraph rootGraph = new(0, scale);
            if (StructureIDs is null)
            {
                //TODO: Retrieve the full network if no structureID's are passed
                return rootGraph;
            }

            List<Structure> listStructures = await LoadStructuresAsync(client, StructureIDs, rootGraph.scale);
            await MorphologyForStructures(Endpoint, rootGraph, listStructures, include_children, rootGraph.scale);

            return rootGraph;
        }

        /// <summary>
        /// Loads the specified Location IDs
        /// </summary>
        /// <param name="LocationIDs"></param>
        /// <param name="Endpoint"></param>
        /// <param name="hops">If > 0, we add additional linked location ID's within N hops of the requested IDs.  Defaults to 0 so only requested IDs are returned.</param>
        /// <returns></returns>
        public static MorphologyGraph FromODataLocationIDs(ICollection<ulong> LocationIDs, Uri Endpoint, int hops = 0)
        {
            return FromODataLocationIDsAsync(LocationIDs, Endpoint, hops).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async overload: loads the specified Location IDs.
        /// </summary>
        public static async Task<MorphologyGraph> FromODataLocationIDsAsync(ICollection<ulong> LocationIDs, Uri Endpoint, int hops = 0)
        {
            ODataClient client = new(Endpoint);

            List<Task<Location>> listLocationFetchTasks = new(LocationIDs?.Count ?? 0);
            if (LocationIDs != null)
            {
                foreach (ulong ID in LocationIDs.Distinct())
                {
                    long lID = (long)ID;
                    Task<Location> t = client.For<Location>().Filter(l => (long)l.ID == lID).FindEntryAsync();
                    listLocationFetchTasks.Add(t);
                }
            }

            var scale = client.GetScale();
            Debug.Assert(scale != null, "We need a scale to do morphology properly");

            MorphologyGraph rootGraph = new(0, scale);
            if (LocationIDs is null || LocationIDs.Count == 0)
            {
                return rootGraph;
            }

            (List<Location> listLocations, Structure Parent, _, bool multipleStructures) = await WaitForLocationsAsync(client, listLocationFetchTasks, scale);
            listLocationFetchTasks.Clear();

            if (multipleStructures)
                throw new NotImplementedException("Multiple structure ID's present in FromODataLocationIDs");
            if (Parent == null)
                throw new InvalidOperationException("FromODataLocationIDs: could not load structure for locations.");

            Parent.LocationLinks = [.. (await CreateLoadStructureLocationLinksTask(client, (long)Parent.ID)).Select(dict => LocationLink.FromDictionary(dict))];

            SortedSet<ulong> LocationsAlreadyRequested = [.. LocationIDs];
            int hopsRemaining = hops;

            while (hopsRemaining > 0)
            {
                SortedSet<ulong> LocationsRequestedThisHop = [];
                hopsRemaining--;

                foreach (var ll in Parent.LocationLinks)
                {
                    bool AddedA = LocationsAlreadyRequested.Contains(ll.A);
                    bool AddedB = LocationsAlreadyRequested.Contains(ll.B);

                    if (AddedA ^ AddedB)
                    {
                        ulong LocationIDToRequest = AddedA ? ll.B : ll.A;

                        if (!LocationsAlreadyRequested.Contains(LocationIDToRequest) && !LocationsRequestedThisHop.Contains(LocationIDToRequest))
                        {
                            long lID = (long)LocationIDToRequest;
                            listLocationFetchTasks.Add(client.For<Location>().Filter(l => (long)l.ID == lID).FindEntryAsync());
                            LocationsRequestedThisHop.Add(LocationIDToRequest);
                        }
                    }
                }

                LocationsAlreadyRequested.UnionWith(LocationsRequestedThisHop);
            }

            (List<Location> listHopLocations, _, _, _) = await WaitForLocationsAsync(client, listLocationFetchTasks, scale);
            listLocations.AddRange(listHopLocations);

            MorphologyGraph graph = new((ulong)Parent.ID, scale, Parent);

            foreach (Location loc in listLocations.Distinct())
            {
                graph.AddNode(new MorphologyNode((ulong)loc.ID, loc, graph));
            }

            AddLocationEdges(graph, [.. Parent.LocationLinks]);

            return graph;
        }

        /// <summary>
        /// A specialized helper function for the FromODataLocationIDs that waits for a list of location tasks to finish downloading
        /// and fires off a request to fetch the locations structure if it is not already present.
        /// </summary>
        private static List<Location> WaitForLocations(Simple.OData.Client.ODataClient client,
                                                        List<Task<Location>> listLocationFetchTasks,
                                                        IScale scale,
                                                        ref long StructureID,
                                                        out Task<Structure> st_task,
                                                        out Task<IEnumerable<IDictionary<string, object>>> st_loc_links_task)
        {
            (List<Location> listLocations, Structure parent, _, _) = WaitForLocationsAsync(client, listLocationFetchTasks, scale).GetAwaiter().GetResult();
            st_task = Task.FromResult(parent);
            st_loc_links_task = null;
            StructureID = parent != null ? (long)parent.ID : 0;
            return listLocations;
        }

        private static async Task<(List<Location> listLocations, Structure Parent, long StructureID, bool multipleStructures)> WaitForLocationsAsync(Simple.OData.Client.ODataClient client,
                                                        List<Task<Location>> listLocationFetchTasks,
                                                        IScale scale)
        {
            if (listLocationFetchTasks == null || listLocationFetchTasks.Count == 0)
                return ([], null, 0, false);

            Location[] locations = await Task.WhenAll(listLocationFetchTasks);
            List<Location> listLocations = [];
            long structureID = 0;
            Structure parent = null;

            foreach (Location l in locations)
            {
                if (l != null)
                {
                    l.scale = scale;
                    listLocations.Add(l);

                    if (structureID == 0)
                    {
                        structureID = (long)l.ParentID;
                        if (client != null)
                        {
                            parent = await client.For<Structure>().Filter(s => (long)s.ID == structureID)
                                .Expand(s => s.Type)
                                .Expand(s => s.Locations)
                                .Expand(s => s.Children)
                                .Expand(s => s.SourceOfLinks)
                                .Expand(s => s.TargetOfLinks).FindEntryAsync();
                        }
                    }
                    else if (structureID != (long)l.ParentID)
                    {
                        return (listLocations, parent, structureID, true);
                    }
                }
            }

            return (listLocations, parent, structureID, false);
        }

        private static void SetLocationScale(IEnumerable<Structure> structures, IScale scale)
        {
            foreach (var s in structures)
            {
                SetLocationScale(s, scale);
            }
        }

        private static void SetLocationScale(Structure s, IScale scale)
        {
            foreach (var l in s.Locations)
            {
                l.scale = scale;
            }
        }


        /// <summary>
        /// Loads the passed structures, or all structures if StructureID's is null
        /// </summary>
        /// <param name="client"></param>
        /// <param name="StructureIDs"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private static async Task<List<Structure>> LoadStructuresOfType(Simple.OData.Client.ODataClient client, long TypeID, IScale scale)
        {
            List<Structure> listStructures = [];

            Task<IEnumerable<Structure>> taskStructures = client.For<Structure>().Filter(s => (long)s.TypeID == TypeID)
                                                            .Expand(s => s.Type)
                                                            //.Expand(s => s.Locations.Select(l => new Location {ID = l.ID, ParentID = l.ParentID, VolumeShape = l.VolumeShape, Z = l.Z, Tags = l.Tags, Terminal = l.Terminal, OffEdge = l.OffEdge}))
                                                            .Expand(s => s.Locations)
                                                            .Expand(s => s.Children)
                                                            .Expand(s => s.SourceOfLinks)
                                                            .Expand(s => s.TargetOfLinks).FindEntriesAsync();


            listStructures = [.. (await taskStructures)];
            SetLocationScale(listStructures, scale);

            await LoadStructureLocationLinks(client, listStructures);

            return listStructures;
        }


        /// <summary>
        /// Loads the passed structures, or all structures if StructureID's is null
        /// </summary>
        /// <param name="client"></param>
        /// <param name="StructureIDs"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private static async Task<List<Structure>> LoadRootStructures(Simple.OData.Client.ODataClient client, IScale scale)
        {
            List<Structure> listStructures = [];

            Task<IEnumerable<Structure>> taskStructures = client.For<Structure>().Filter(s => s.ParentID == null)
                                                            .Expand(s => s.Type)
                                                            //.Expand(s => s.Locations.Select(l => new Location {ID = l.ID, ParentID = l.ParentID, VolumeShape = l.VolumeShape, Z = l.Z, Tags = l.Tags, Terminal = l.Terminal, OffEdge = l.OffEdge}))
                                                            .Expand(s => s.Locations)
                                                            .Expand(s => s.Children)
                                                            .Expand(s => s.SourceOfLinks)
                                                            .Expand(s => s.TargetOfLinks).FindEntriesAsync();

            listStructures = [.. (await taskStructures)];
            SetLocationScale(listStructures, scale);
            await LoadStructureLocationLinks(client, listStructures);

            return listStructures;
        }

        /// <summary>
        /// Loads the passed structures, or all structures if StructureID's is null
        /// </summary>
        /// <param name="client"></param>
        /// <param name="StructureIDs"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private static List<Structure> LoadStructures(Simple.OData.Client.ODataClient client, IEnumerable<ulong> StructureIDs, UnitsAndScale.IScale scale)
        {
            return LoadStructuresAsync(client, StructureIDs, scale).GetAwaiter().GetResult();
        }

        private static async Task<List<Structure>> LoadStructuresAsync(Simple.OData.Client.ODataClient client, IEnumerable<ulong> StructureIDs, UnitsAndScale.IScale scale)
        {
            List<Structure> listStructures = [];

            if (StructureIDs is null)
            {
                return listStructures;
            }

            List<ulong> idList = [.. StructureIDs];
            List<Task<Structure>> listTasks = [];
            foreach (ulong ID in idList)
            {
                long sID = (long)ID;
                Task<Structure> t = client.For<Structure>().Filter(s => (long)s.ID == sID)
                    .Expand(s => s.Type)
                    .Expand(s => s.Locations)
                    .Expand(s => s.Children)
                    .Expand(s => s.SourceOfLinks)
                    .Expand(s => s.TargetOfLinks).FindEntryAsync();
                listTasks.Add(t);
            }

            SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> st_loc_link_tasks = CreateLoadStructureLocationLinksTasks(client, idList);

            Structure[] structures = await Task.WhenAll(listTasks);

            foreach (Structure s in structures)
            {
                if (s != null && s.Locations != null)
                {
                    foreach (Location l in s.Locations)
                    {
                        l.scale = scale;
                    }

                    listStructures.Add(s);

                    if (st_loc_link_tasks.TryGetValue(s.ID, out var loc_link_task))
                    {
                        IEnumerable<IDictionary<string, object>> links = await loc_link_task;
                        s.LocationLinks = [.. links.Select(dict => LocationLink.FromDictionary(dict))];
                    }
                }
            }

            return listStructures;
        }

        private static async Task SetLocationLinksFromTaskAsync(Structure s, Simple.OData.Client.ODataClient client)
        {
            try
            {
                var links = await client.ExecuteFunctionAsArrayAsync<LocationLink>("StructureLocationLinks", new Dictionary<string, object>() { { "StructureID", System.Convert.ToInt64(s.ID) } });
                s.LocationLinks = links ?? Array.Empty<LocationLink>();
            }
            catch
            {
                s.LocationLinks = Array.Empty<LocationLink>();
            }
        }

        /// <summary>
        /// Populates the passed structure objects with all location links for all child locations
        /// </summary>
        /// <param name="client"></param>
        /// <param name="structures"></param>
        private static Task LoadStructureLocationLinks(Simple.OData.Client.ODataClient client, ICollection<Structure> structures)
        {
            List<Task> tasks = new(structures.Count);
            foreach (Structure s in structures)
            {
                if (s.LocationLinks is null)
                {
                    tasks.Add(SetLocationLinksFromTaskAsync(s, client));
                }
            }

            return Task.WhenAll([.. tasks]);
            /*
            foreach(Structure s in structures)
            {
                if (!tasks.ContainsKey(s.ID))
                {
                    continue;
                }

                Task<LocationLink[]> T = tasks[s.ID];
                if(!T.IsFaulted)
                {
                    s.LocationLinks = T.Result;
                }
            }
            */
        }

        /// <summary>
        /// Populates the passed structure objects with all location links for all child locations
        /// </summary>
        /// <param name="client"></param>
        /// <param name="structures"></param>
        private static SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> CreateLoadStructureLocationLinksTasks(Simple.OData.Client.ODataClient client, IEnumerable<ulong> structureIDs)
        {
            SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> tasks = [];
            foreach (ulong structureID in structureIDs)
            {
                long sID = (long)structureID; //The OData client we use doesn't support ulong...
                Task<IEnumerable<IDictionary<string, object>>> t = CreateLoadStructureLocationLinksTask(client, sID);
                tasks.Add(structureID, t);
            }

            return tasks;
        }

        /// <summary>
        /// Populates the passed structure objects with all location links for all child locations
        /// </summary>
        /// <param name="client"></param>
        /// <param name="structures"></param>
        private static Task<IEnumerable<IDictionary<string, object>>> CreateLoadStructureLocationLinksTask(Simple.OData.Client.ODataClient client, long structureID) => client.FindEntriesAsync(string.Format("StructureLocationLinks(StructureID={0})?$select=A,B", structureID));

        /// <summary>
        /// Add the morphology for the passed structure ID to the provided root graph
        /// </summary>
        /// <param name="rootGraph"></param>
        /// <param name="StructureIDs"></param>
        private static async Task MorphologyForStructures(Uri Endpoint, MorphologyGraph rootGraph, ICollection<Structure> Structures, bool include_children, UnitsAndScale.IScale scale)
        {
            //Queries.PopulateStructureTypes();

            // Get the nodes and build graph for numHops            
            //            System.Threading.Tasks.Parallel.ForEach<Structure>(Structures, s =>

            foreach (Structure s in Structures)
            {
                MorphologyGraph graph = MorphologyGraphForStructure(s, scale);
                if (graph is null)
                    return;

                rootGraph.AddSubgraph(graph);

                if (include_children && s.Children != null && s.Children.Any())
                {
                    //Optimization, use the already loaded StructureTypes instead of expand
                    //MorphologyGraph subgraph = await FromOData(s.Children.Select(child => System.Convert.ToInt64(child.ID)).ToList(), include_children, Endpoint);
                    //graph.AddSubgraph(subgraph);

                    List<Structure> child_structs = await LoadStructuresAsync(new Simple.OData.Client.ODataClient(Endpoint), [.. s.Children.Select(child => System.Convert.ToUInt64(child.ID))], scale);
                    await MorphologyForStructures(Endpoint, graph, child_structs, include_children, scale);

                    //IList<Structure> child_structs = client.Structures.Expand(st => st.Locations).Expand(st => st.Type).Expand(st => st.Children).Where(st => st.ParentID == s.ID).ToList();
                    //LoadStructureLocationLinks(container, child_structs);
                    //MorphologyForStructures(container, graph, child_structs, include_children, scale);
                }
            }
            //);
        }

        private static MorphologyGraph MorphologyGraphForStructure(Structure s, UnitsAndScale.IScale scale)
        {
            if (s.Locations is null)
                return null;

            Location[] locations = [.. s.Locations];
            LocationLink[] location_links = [.. s.LocationLinks];

            if (locations.Length <= 0)
            {
                return null;
            }

            MorphologyGraph graph = new((ulong)s.ID, scale, s);
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
            if (location_links is null)
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
