using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnitsAndScale;

namespace AnnotationVizLib.SimpleOData
{
    public static class SimpleODataMorphologyFactory
    {
        /// <summary>
        /// Retrieve the morphology graph for all structures
        /// </summary>
        /// <param name="Endpoint"></param>
        /// <returns></returns>
        public static MorphologyGraph FromOData(Uri Endpoint, bool include_children)
        {
            var client = new Simple.OData.Client.ODataClient(Endpoint);

            var scale = client.GetScale();
            Debug.Assert(scale != null);

            MorphologyGraph rootGraph = new MorphologyGraph(0, scale);

            List<Structure> listStructures = LoadRootStructures(client, rootGraph.scale);
            MorphologyForStructures(Endpoint, rootGraph, listStructures, include_children, rootGraph.scale);

            return rootGraph;
        }

        public static MorphologyGraph FromOData(ICollection<ulong> StructureIDs, bool include_children, Uri Endpoint)
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

        /// <summary>
        /// Loads the specified Location IDs
        /// </summary>
        /// <param name="LocationIDs"></param>
        /// <param name="Endpoint"></param>
        /// <param name="hops">If > 0, we add additional linked location ID's within N hops of the requested IDs.  Defaults to 0 so only requested IDs are returned.</param>
        /// <returns></returns>
        public static MorphologyGraph FromODataLocationIDs(ICollection<ulong> LocationIDs, Uri Endpoint, int hops = 0)
        {
            var client = new Simple.OData.Client.ODataClient(Endpoint);

            ///////////////////////////////////////
            //Download the initial set of locations
            List<Task<Location>> listLocationFetchTasks = new List<Task<SimpleOData.Location>>(LocationIDs.Count);

            foreach (ulong ID in LocationIDs.Distinct())
            {
                long lID = (long)ID; //ulong is not supported by the library so we need to cast
                Task<Location> t = client.For<Location>().Filter(l => (long)l.ID == lID).FindEntryAsync();
                listLocationFetchTasks.Add(t);
            }

            /////////////////////////////////////////////////////////////////////////////
            //Run tasks (fetch scale and structure) in parallel during location download
            var scale = client.GetScale();
            Debug.Assert(scale != null, "We need a scale to do morphology properly");

            /////////////////////////////////////////////////////////////////////////////////////
            //Check if any work was actually requested, if not return empty graph with scale data
            MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
            if (LocationIDs == null || LocationIDs.Count() == 0)
            {
                //TODO: Retrieve the full network if no structureID's are passed
                return rootGraph;
            }

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            //Wait for first set of locations to download, but start the request for structure as soon as possible. 

            long StructureID = 0;
            Task<Structure> st_task = null; //This is the task that will fetch the structure once we identify it
            Task<IEnumerable<IDictionary<string, object>>> st_loc_links_task = null;

            List<Location> listLocations = WaitForLocations(client, listLocationFetchTasks, scale, ref StructureID, out st_task, out st_loc_links_task);
            listLocationFetchTasks.Clear();


            //////////////////////////////////////
            //First set of locations is downloaded
            //Need the structure request to finish before we load locations
            st_task.Wait();
            Structure Parent = st_task.Result;

            st_loc_links_task.Wait();
            Parent.LocationLinks = st_loc_links_task.Result.Select(dict => LocationLink.FromDictionary(dict)).ToArray();

            /////////////////////////////////////////////////////////////
            //Request additional locationID's based on the number of hops
            SortedSet<ulong> LocationsAlreadyRequested = new SortedSet<ulong>(LocationIDs);

            while (hops > 0)
            {
                SortedSet<ulong> LocationsRequestedThisHop = new SortedSet<ulong>();
                hops = hops - 1;

                foreach (var ll in Parent.LocationLinks)
                {
                    bool AddedA = LocationsAlreadyRequested.Contains(ll.A);
                    bool AddedB = LocationsAlreadyRequested.Contains(ll.B);

                    if (AddedA ^ AddedB)
                    {
                        ulong LocationIDToRequest = AddedA ? ll.B : ll.A;

                        if (false == LocationsAlreadyRequested.Contains(LocationIDToRequest) && false == LocationsRequestedThisHop.Contains(LocationIDToRequest))
                        {
                            long lID = (long)LocationIDToRequest; //The OData client we use does not understand ulong
                            Task<Location> t = client.For<Location>().Filter(l => (long)l.ID == lID).FindEntryAsync();
                            listLocationFetchTasks.Add(t);
                            LocationsRequestedThisHop.Add(LocationIDToRequest);
                        }
                    }
                }

                LocationsAlreadyRequested.UnionWith(LocationsRequestedThisHop);
            }

            ////////////////////////////////////////////////////////// 
            List<Location> listHopLocations = WaitForLocations(client, listLocationFetchTasks, scale, ref StructureID, out st_task, out st_loc_links_task);
            listLocations.AddRange(listHopLocations);

            MorphologyGraph graph = new MorphologyGraph((ulong)Parent.ID, scale, Parent);

            //LoadStructureLocationLinks(client, new Structure[] { Parent });

            foreach (Location loc in listLocations.Distinct())
            {
                //TODO: REMOVE Z * 10
                //loc.Z *= 10;
                graph.AddNode(new MorphologyNode((ulong)loc.ID, loc, graph));
            }

            AddLocationEdges(graph, Parent.LocationLinks.ToArray());

            return graph;
        }

        /// <summary>
        /// A specialized helper function for the FromODataLocationIDs that waits for a list of location tasks to finish downloading 
        /// and fires off a request to fetch the locations structure if it is not already present.
        /// </summary>
        /// <param name="client">Client for structure request.  If null structure will not be requested.</param>
        /// <param name="listLocationFetchTasks">Tasks we will wait on</param>
        /// <param name="scale">Scale to add to Location objects</param>
        /// <param name="StructureID">ID of structure, if 0 StructureID is unknown and a request will be sent when it is known</param>
        /// <param name="st">Task we created to download structure, if any</param>
        /// <returns></returns>
        private static List<Location> WaitForLocations(Simple.OData.Client.ODataClient client,
                                                        List<Task<Location>> listLocationFetchTasks,
                                                        IScale scale,
                                                        ref long StructureID,
                                                        out Task<Structure> st_task,
                                                        out Task<IEnumerable<IDictionary<string, object>>> st_loc_links_task)
        {
            st_task = null;
            st_loc_links_task = null;
            List<Location> listLocations = new List<Location>();
            var locationFetchTasksArray = listLocationFetchTasks.ToArray();

            while (listLocationFetchTasks.Count > 0)
            {
                int iFinishedTask = Task.WaitAny(locationFetchTasksArray);
                var t = listLocationFetchTasks[iFinishedTask];

                listLocationFetchTasks.RemoveAt(iFinishedTask);
                locationFetchTasksArray = locationFetchTasksArray.RemoveAt(iFinishedTask);

                Location l = t.Result;
                if (l != null)
                {
                    l.scale = scale;
                    listLocations.Add(l);

                    if (StructureID == 0)
                    {
                        StructureID = (long)l.ParentID;

                        long sID = StructureID;

                        if (client != null)
                        {
                            //Begin the request for the structure.
                            st_task = client.For<Structure>().Filter(s => (long)s.ID == sID).FindEntryAsync();
                            st_loc_links_task = CreateLoadStructureLocationLinksTask(client, sID);
                        }
                    }
                    else if (StructureID != (long)l.ParentID)
                    {
                        throw new NotImplementedException("Multiple structure ID's present in FromODataLocationIDs");
                    }
                }
            }

            return listLocations;
        }
        /// <summary>
        /// Loads the passed structures, or all structures if StructureID's is null
        /// </summary>
        /// <param name="client"></param>
        /// <param name="StructureIDs"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private static List<Structure> LoadRootStructures(Simple.OData.Client.ODataClient client, IScale scale)
        {
            List<Structure> listStructures = new List<Structure>();

            Task<IEnumerable<Structure>> taskStructures = client.For<Structure>().Filter(s => s.ParentID == null)
                                                            .Expand(s => s.Type)
                                                            //.Expand(s => s.Locations.Select(l => new Location {ID = l.ID, ParentID = l.ParentID, VolumeShape = l.VolumeShape, Z = l.Z, Tags = l.Tags, Terminal = l.Terminal, OffEdge = l.OffEdge}))
                                                            .Expand(s => s.Locations)
                                                            .Expand(s => s.Children)
                                                            .Expand(s => s.SourceOfLinks)
                                                            .Expand(s => s.TargetOfLinks).FindEntriesAsync();

            taskStructures.Wait();

            listStructures = taskStructures.Result.ToList();
            LoadStructureLocationLinks(client, listStructures);

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
            List<Structure> listStructures = new List<Structure>();

            if (StructureIDs == null)
            {
                return listStructures;
            }

            List<Task<Structure>> listTasks = new List<Task<SimpleOData.Structure>>();
            SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> st_loc_link_tasks = new SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>>();

            foreach (ulong ID in StructureIDs)
            {
                long sID = (long)ID;
                Task<Structure> t = client.For<Structure>().Filter(s => (long)s.ID == sID)
                                                            .Expand(s => s.Type)
                                                            //.Expand(s => s.Locations.Select(l => new Location {ID = l.ID, ParentID = l.ParentID, VolumeShape = l.VolumeShape, Z = l.Z, Tags = l.Tags, Terminal = l.Terminal, OffEdge = l.OffEdge}))
                                                            .Expand(s => s.Locations)
                                                            .Expand(s => s.Children)
                                                            .Expand(s => s.SourceOfLinks)
                                                            .Expand(s => s.TargetOfLinks).FindEntryAsync();
                listTasks.Add(t);
            }

            //////////////////////////////////////////////////////////////////////////////////////////
            //Create tasks to fetch the structure location links before waiting on our structure tasks
            st_loc_link_tasks = CreateLoadStructureLocationLinksTasks(client, StructureIDs);

            Task<Structure>[] taskArray = listTasks.ToArray();
            while (taskArray.Length > 0)
            {
                int iFinished = Task.WaitAny(taskArray);
                Task<Structure> t = taskArray[iFinished];
                Structure s = t.Result;

                taskArray = taskArray.RemoveAt(iFinished);
                listTasks.RemoveAt(iFinished);

                if (s != null && s.Locations != null)
                {
                    foreach (Location l in s.Locations)
                    {
                        l.scale = scale;
                    }

                    listStructures.Add(s);
                }
            }

            foreach (Structure s in listStructures)
            {
                if (!st_loc_link_tasks.ContainsKey(s.ID))
                {
                    continue;
                }

                Task<IEnumerable<IDictionary<string, object>>> T = st_loc_link_tasks[s.ID];
                T.Wait();
                if (!T.IsFaulted)
                {
                    s.LocationLinks = T.Result.Select(dict => LocationLink.FromDictionary(dict)).ToArray();
                }

                st_loc_link_tasks.Remove(s.ID);
            }

            return listStructures;
        }

        /// <summary>
        /// Populates the passed structure objects with all location links for all child locations
        /// </summary>
        /// <param name="client"></param>
        /// <param name="structures"></param>
        private static void LoadStructureLocationLinks(Simple.OData.Client.ODataClient client, IEnumerable<Structure> structures)
        {
            SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> tasks = new SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>>();

            ulong[] StructuresToRequest = structures.Where(s => s.LocationLinks == null).Select(s => s.ID).Distinct().ToArray();
            tasks = CreateLoadStructureLocationLinksTasks(client, StructuresToRequest);

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
        /// Populates the passed structure objects with all location links for all child locations
        /// </summary>
        /// <param name="client"></param>
        /// <param name="structures"></param>
        private static SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> CreateLoadStructureLocationLinksTasks(Simple.OData.Client.ODataClient client, IEnumerable<ulong> structureIDs)
        {
            SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>> tasks = new SortedList<ulong, Task<IEnumerable<IDictionary<string, object>>>>();
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
        private static Task<IEnumerable<IDictionary<string, object>>> CreateLoadStructureLocationLinksTask(Simple.OData.Client.ODataClient client, long structureID)
        {
            return client.FindEntriesAsync(string.Format("StructureLocationLinks(StructureID={0})?$select=A,B", structureID));
        }

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
                MorphologyGraph graph = MorphologyForStructure(s, scale);
                if (graph == null)
                    return;

                rootGraph.AddSubgraph(graph);

                if (include_children && s.Children != null && s.Children.Any())
                {
                    //Optimization, use the already loaded StructureTypes instead of expand
                    //MorphologyGraph subgraph = await FromOData(s.Children.Select(child => System.Convert.ToInt64(child.ID)).ToList(), include_children, Endpoint);
                    //graph.AddSubgraph(subgraph);

                    List<Structure> child_structs = LoadStructures(new Simple.OData.Client.ODataClient(Endpoint), s.Children.Select(child => System.Convert.ToUInt64(child.ID)).ToArray(), scale);
                    await MorphologyForStructures(Endpoint, graph, child_structs, include_children, scale);

                    //IList<Structure> child_structs = client.Structures.Expand(st => st.Locations).Expand(st => st.Type).Expand(st => st.Children).Where(st => st.ParentID == s.ID).ToList();
                    //LoadStructureLocationLinks(container, child_structs);
                    //MorphologyForStructures(container, graph, child_structs, include_children, scale);
                }
            }
            //);
        }

        private static MorphologyGraph MorphologyForStructure(Structure s, UnitsAndScale.IScale scale)
        {
            if (s.Locations == null)
                return null;

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
