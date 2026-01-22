
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace ConnectomeDataModel
{
    public readonly struct NetworkDetails(Structure[] nodes, Structure[] childNodes, StructureLink[] edges)
    {
        public readonly Structure[] Nodes = nodes;
        public readonly Structure[] ChildNodes = childNodes;
        public readonly StructureLink[] Edges = edges;
    }

    public readonly struct AnnotationCollection(IDictionary<long, Structure> structs, IDictionary<long, Location> locs)
    {
        public readonly IDictionary<long, Structure> Structures = structs;
        public readonly IDictionary<long, Location> Locations = locs;
    }

    /// <summary>
    /// User to hold data from a DBReader as SQLGeometry objects are converted on other threads
    /// </summary>
    public class UnconvertedStructureSpatialCache
    {
        public StructureSpatialCache row = new();
        public System.Threading.Tasks.Task<System.Data.Entity.Spatial.DbGeometry> ConvexHullTask = null;
        public System.Threading.Tasks.Task<System.Data.Entity.Spatial.DbGeometry> BBoxTask = null;

        private static System.Data.Entity.Spatial.DbGeometry UnpackSqlGeometry(Microsoft.SqlServer.Types.SqlGeometry input) =>
            //return System.Data.Entity.Spatial.DbGeometry.FromBinary(input.STAsBinary().Buffer);
            System.Data.Entity.Spatial.DbGeometry.FromText(input.ToString());

        public static UnconvertedStructureSpatialCache PopulateAsync(System.Data.Common.DbDataReader reader)
        {
            UnconvertedStructureSpatialCache obj = new();
            obj.row.ID = reader.GetInt64(0);
            //row.BoundingRect = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1).STAsBinary().Buffer);
            //obj.row.BoundingRect = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1).STAsBinary().Buffer);
            Microsoft.SqlServer.Types.SqlGeometry bbox_input = reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1);
            obj.BBoxTask = Task<System.Data.Entity.Spatial.DbGeometry>.Run(() => UnpackSqlGeometry(bbox_input));
            obj.row.Area = reader.GetDouble(2);
            obj.row.Volume = reader.GetDouble(3);
            obj.row.MaxDimension = reader.GetInt32(4);
            obj.row.MinZ = reader.GetDouble(5);
            obj.row.MaxZ = reader.GetDouble(6);
            //row.ConvexHull = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7).STAsBinary().Buffer);
            //row.ConvexHull = System.Data.Entity.Spatial.DbGeometry.FromText(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7).ToString());
            Microsoft.SqlServer.Types.SqlGeometry convex_hull_input = reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7);
            obj.ConvexHullTask = Task<System.Data.Entity.Spatial.DbGeometry>.Run(() => UnpackSqlGeometry(convex_hull_input));
            obj.row.LastModified = reader.GetDateTime(8);

            return obj;
        }
        /// <summary>
        /// Waits for the tasks to return, returns the final object
        /// </summary>
        /// <returns></returns>
        public StructureSpatialCache WaitReturn()
        {
            row.BoundingRect = BBoxTask.Result;
            row.ConvexHull = ConvexHullTask.Result;
            return row;
        }
    }


    public partial class ConnectomeEntities
    {
        public void ConfigureAsReadOnly()
        {
            //Note, disabling LazyLoading breaks loading of children and links unless they have been populated previously.
            this.Database.CommandTimeout = 90;
            this.Configuration.LazyLoadingEnabled = false;
            this.Configuration.UseDatabaseNullSemantics = true;
            this.Configuration.AutoDetectChangesEnabled = false;
        }

        public void ConfigureAsReadOnlyWithLazyLoading()
        {
            //Note, disabling LazyLoading breaks loading of children and links unless they have been populated previously.
            this.Database.CommandTimeout = 90;
            this.Configuration.LazyLoadingEnabled = true;
            this.Configuration.UseDatabaseNullSemantics = true;
            this.Configuration.AutoDetectChangesEnabled = false;
        }

        /// <summary>
        /// Our server didn't exist before 2007 and if we pass a date earlier than 1753 the SQL query fails
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static DateTime? ValidateDate(DateTime? input)
        {
            if (input.HasValue == false)
                return input;

            if (input < new DateTime(2000, 1, 1))
                return new DateTime(2000, 1, 1);
            else
                return input;
        }

        private static SqlParameter CreateSectionNumberParameter(long section)
        {
            SqlParameter param = new("Z", System.Data.SqlDbType.Float)
            {
                Direction = System.Data.ParameterDirection.Input,
                SqlValue = new System.Data.SqlTypes.SqlDouble((double)section)
            };

            return param;
        }



        private static SqlParameter CreateMinRadiusParameter(double MinRadius)
        {
            SqlParameter param = new("MinRadius", System.Data.SqlDbType.Float)
            {
                Direction = System.Data.ParameterDirection.Input,
                SqlValue = new System.Data.SqlTypes.SqlDouble((double)MinRadius)
            };

            return param;
        }

        private static SqlParameter CreateBoundingBoxParameter(System.Data.Entity.Spatial.DbGeometry bbox)
        {

            System.Data.SqlDbType dbGeoType = System.Data.SqlDbType.Udt;
            SqlParameter param = new("BBox", dbGeoType)
            {
                UdtTypeName = "geometry",
                Direction = System.Data.ParameterDirection.Input,
                SqlValue = bbox
            };

            return param;
        }

        private static SqlParameter CreateDateTimeParameter(DateTime? time)
        {
            SqlParameter param = new("QueryDate", System.Data.SqlDbType.DateTime)
            {
                Direction = System.Data.ParameterDirection.Input
            };

            param.SqlValue = !time.HasValue ? DBNull.Value : new System.Data.SqlTypes.SqlDateTime(time.Value);
            return param;
        }

        /// <summary>
        /// Return parents of all structures which have a link.  Used to identify every node required to construct the complete connectivity network
        /// </summary>
        /// <param name="proxy"></param>
        /// <returns></returns>
        public IQueryable<long> GetLinkedStructureParentIDs()
        {
            IQueryable<long> LinkedStructureIDs = this.StructureLinks.Select(L => L.SourceID).
                                                    Union(this.StructureLinks.Select(L => L.TargetID)).Distinct();

            IQueryable<long> LinkedStructureParentIDs = this.Structures.Join<Structure, StructureLink, long, long?>(this.StructureLinks,
                                                                             s => s.ID,
                                                                             sls => sls.SourceID,
                                                                             (s, sls) => s.ParentID)
                                                                             .Distinct()
                                                        .Union(
                                                            this.Structures.Join<Structure, StructureLink, long, long?>(this.StructureLinks,
                                                                             s => s.ID,
                                                                             sls => sls.TargetID,
                                                                             (s, sls) => s.ParentID)
                                                        //.Distinct()
                                                        ).Distinct()
                                                        .Where(ParentID => ParentID.HasValue)
                                                        .Select(ParentID => ParentID.Value);

            return LinkedStructureParentIDs;
        }


        public IQueryable<Location> ReadSectionLocations(long section, DateTime? LastModified)
        {
            if (LastModified.HasValue)
            {
                return this.SectionLocations((double)section).Where(l => l.LastModified >= LastModified.Value);
            }
            else
            {
                return this.SectionLocations((double)section);
            }
        }

        public IList<Location> ReadSectionLocationsAndLinks(long section, DateTime? LastModified)
        {
            var results = this.SelectSectionLocationsAndLinks((double)section, LastModified, MergeOption.NoTracking);

            Dictionary<long, Location> dictLocations = results.ToDictionary(l => l.ID);

            List<LocationLink> LocationLinks = [.. results.GetNextResult<LocationLink>()];

            AppendLinksToLocations(dictLocations, LocationLinks);

            return [.. dictLocations.Values];
        }

        public IList<Location> ReadSectionLocationsAndLinksInMosaicRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionLocationsAndLinksInMosaicBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Location> dictLocations = results.ToDictionary(l => l.ID);

            List<LocationLink> LocationLinks = [.. results.GetNextResult<LocationLink>()];

            AppendLinksToLocations(dictLocations, LocationLinks);

            return [.. dictLocations.Values];
        }

        public IList<Location> ReadSectionLocationsAndLinksInVolumeRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionLocationsAndLinksInVolumeBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Location> dictLocations = results.ToDictionary(l => l.ID);

            List<LocationLink> LocationLinks = [.. results.GetNextResult<LocationLink>()];

            AppendLinksToLocations(dictLocations, LocationLinks);

            return [.. dictLocations.Values];
        }

        public IList<Location> ReadStructureLocationsAndLinks(long StructureID)
        {
            var results = this.SelectStructureLocationsAndLinks(StructureID);

            Dictionary<long, Location> dictLocations = results.ToDictionary(l => l.ID);
            List<LocationLink> LocationLinks = [.. results.GetNextResult<LocationLink>()];

            AppendLinksToLocations(dictLocations, LocationLinks);

            return [.. dictLocations.Values];
        }

        public IList<Structure> ReadSectionStructuresAndLinks(long section, DateTime? LastModified)
        {
            var results = this.SelectSectionStructuresAndLinks((double)section, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            List<StructureLink> StructureLinks = [.. results.GetNextResult<StructureLink>()];

            AppendLinksToStructures(dictStructures, StructureLinks);

            return [.. dictStructures.Values];
        }

        public IList<Structure> ReadSectionStructuresAndLinksInMosaicRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionStructuresAndLinksInMosaicBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            List<StructureLink> StructureLinks = [.. results.GetNextResult<StructureLink>()];

            AppendLinksToStructures(dictStructures, StructureLinks);

            return [.. dictStructures.Values];
        }

        public IList<Structure> ReadSectionStructuresAndLinksInVolumeRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionStructuresAndLinksInVolumeBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            List<StructureLink> StructureLinks = [.. results.GetNextResult<StructureLink>()];

            AppendLinksToStructures(dictStructures, StructureLinks);

            return [.. dictStructures.Values];
        }


        public AnnotationCollection ReadSectionAnnotationsInMosaicRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionAnnotationsInMosaicBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>();

            AppendLinksToStructures(dictStructures, [.. StructureLinks]);

            var Locations = StructureLinks.GetNextResult<Location>();

            Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);

            var LocationLinks = Locations.GetNextResult<LocationLink>();

            AppendLinksToLocations(dictLocations, [.. LocationLinks]);

            return new AnnotationCollection(dictStructures, dictLocations);
        }

        public AnnotationCollection ReadSectionAnnotationsInVolumeRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionAnnotationsInVolumeBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>();

            AppendLinksToStructures(dictStructures, [.. StructureLinks]);

            var Locations = StructureLinks.GetNextResult<Location>();

            Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);

            var LocationLinks = Locations.GetNextResult<LocationLink>();

            AppendLinksToLocations(dictLocations, [.. LocationLinks]);

            return new AnnotationCollection(dictStructures, dictLocations);
        }

        public virtual int SplitStructure(long keepStructureID, long firstLocationIDOfSplitStructure, out long NewStructureID)
        {
            ObjectParameter keepStructureIDParameter = new("KeepStructureID", keepStructureID);
            ObjectParameter firstLocationIDOfSplitStructureParameter = new("FirstLocationIDOfSplitStructure", firstLocationIDOfSplitStructure);
            ObjectParameter NewStructureIDParam = new("SplitStructureID", typeof(long));

            int retval = ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("SplitStructure", keepStructureIDParameter, firstLocationIDOfSplitStructureParameter, NewStructureIDParam);
            NewStructureID = retval != 0 ? -1 : (long)NewStructureIDParam.Value;

            return retval;
        }

        public virtual int SplitStructureAtLocationLink(long LocationIDOfKeepStructure, long LocationIDOfSplitStructure, out long NewStructureID)
        {
            ObjectParameter LocationIDOfKeepStructureParameter = new("LocationIDOfKeepStructure", LocationIDOfKeepStructure);
            ObjectParameter LocationIDOfSplitStructureParameter = new("LocationIDOfSplitStructure", LocationIDOfSplitStructure);
            ObjectParameter NewStructureIDParam = new("SplitStructureID", typeof(long));

            int retval = ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("SplitStructureAtLocationLink", LocationIDOfKeepStructureParameter, LocationIDOfSplitStructureParameter, NewStructureIDParam);
            NewStructureID = retval != 0 ? -1 : (long)NewStructureIDParam.Value;

            return retval;
        }


        public SortedSet<long> SelectNetworkStructureIDs(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkStructureIDsStoredProcedure proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            SortedSet<long> StructureIDs = [.. EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteStoredProcedure<long>(this.Database, proc)];
            return StructureIDs;
        }

        public NetworkDetails SelectNetworkDetails(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkDetailsStoredProcedure proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            NetworkDetails retval;

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                Structure[] NodeObjects = [.. ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking)];
                reader.NextResult();
                Structure[] ChildObjects = [.. ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking)];
                reader.NextResult();
                StructureLink[] Edges = [.. ((IObjectContextAdapter)this).ObjectContext.Translate<StructureLink>(reader, "StructureLinks", MergeOption.NoTracking)];

                retval = new NetworkDetails(NodeObjects, ChildObjects, Edges);
            }

            this.Database.Connection.Close();

            return retval;
        }

        public IQueryable<Structure> SelectNetworkStructures(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkStructuresProcedure proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            Structure[] NodeObjects;
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                NodeObjects = [.. ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking)];
            }

            this.Database.Connection.Close();

            return NodeObjects.AsQueryable<Structure>();
        }

        public IQueryable<Structure> SelectNetworkChildStructuresIDs(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkChildStructureIDsProcedure proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            SortedSet<long> ChildStructureIDs = [.. EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteStoredProcedure<long>(this.Database, proc)];

            return from s in this.Structures
                   where ChildStructureIDs.Contains(s.ID)
                   select s;
        }

        public IQueryable<StructureSpatialCache> SelectNetworkStructureSpatialData(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkStructureSpatialData proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            List<StructureSpatialCache> NodeObjects;
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                NodeObjects = ConvertReaderToList(reader);
                //NodeObjects = ((IObjectContextAdapter)this).ObjectContext.Translate<StructureSpatialCache>(reader, "StructureSpatialCaches", MergeOption.NoTracking).ToArray();
            }

            this.Database.Connection.Close();

            return NodeObjects.AsQueryable<StructureSpatialCache>();
            /*
            SortedSet<long> ChildStructureIDs = new SortedSet<long>(EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteStoredProcedure<long>(this.Database, proc));

            return from s in this.StructureSpatialCaches
                   where ChildStructureIDs.Contains(s.ID)
                   select s;*/
        }

        public IQueryable<StructureSpatialCache> SelectNetworkChildStructureSpatialData(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkChildStructureSpatialData proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            List<StructureSpatialCache> NodeObjects = [];
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                NodeObjects = ConvertReaderToList(reader);
                // NodeObjects = ((IObjectContextAdapter)this).ObjectContext.Translate<StructureSpatialCache>(reader, "StructureSpatialCaches", MergeOption.NoTracking).ToArray();
            }

            this.Database.Connection.Close();

            return NodeObjects.AsQueryable<StructureSpatialCache>();
            /*
            SortedSet<long> ChildStructureIDs = new SortedSet<long>(EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteStoredProcedure<long>(this.Database, proc));

            return from s in this.StructureSpatialCaches
                   where ChildStructureIDs.Contains(s.ID)
                   select s;
                   */
        }

        public List<StructureSpatialCache> ConvertReaderToList(System.Data.Common.DbDataReader reader)
        {
            List<UnconvertedStructureSpatialCache> NodeObjects = [];

            while (reader.Read())
            {
                UnconvertedStructureSpatialCache row = UnconvertedStructureSpatialCache.PopulateAsync(reader);
                /*
                StructureSpatialCache row = new StructureSpatialCache();
                row.ID = reader.GetInt64(0);
                //row.BoundingRect = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1).STAsBinary().Buffer);
                row.BoundingRect = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1).STAsBinary().Buffer);
                row.Area = reader.GetDouble(2);
                row.Volume = reader.GetDouble(3);
                row.MaxDimension = reader.GetInt32(4);
                row.MinZ = reader.GetDouble(5);
                row.MaxZ = reader.GetDouble(6);
                //row.ConvexHull = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7).STAsBinary().Buffer);
                row.ConvexHull = System.Data.Entity.Spatial.DbGeometry.FromText(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7).ToString());
                row.LastModified = reader.GetDateTime(8);
                */

                NodeObjects.Add(row);
            }

            return [.. NodeObjects.Select(o => o.WaitReturn())];
        }

        public IQueryable<Structure> SelectNetworkChildStructures(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkChildStructuresProcedure proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            Structure[] ChildStructures;
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                ChildStructures = [.. ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking)];
            }

            this.Database.Connection.Close();

            return ChildStructures.AsQueryable<Structure>();
        }

        public IQueryable<StructureLink> SelectNetworkStructureLinks(IEnumerable<long> IDs, int numHops)
        {
            SelectNetworkStructureLinksProcedure proc = new()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            StructureLink[] Links;
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                Links = [.. ((IObjectContextAdapter)this).ObjectContext.Translate<StructureLink>(reader, "StructureLinks", MergeOption.NoTracking)];
            }

            this.Database.Connection.Close();

            return Links.AsQueryable<StructureLink>();
        }




        /// <summary>
        /// Add the links to the locations in the dictionary
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="LocationLinks"></param>
        public void AppendLinksToStructures(IDictionary<long, Structure> Structures, IList<StructureLink> StructureLinks)
        {
            foreach (StructureLink link in StructureLinks)
            {
                if (Structures.TryGetValue(link.SourceID, out Structure Source))
                {
                    Source.SourceOfLinks.Add(link);
                }

                if (Structures.TryGetValue(link.TargetID, out Structure Target))
                {
                    Target.TargetOfLinks.Add(link);
                }
            }
        }


        /// <summary>
        /// Add the links to the locations in the dictionary
        /// </summary>
        /// <param name="Locations"></param>
        /// <param name="LocationLinks"></param>
        public void AppendLinksToLocations(IDictionary<long, Location> Locations, IList<LocationLink> LocationLinks)
        {
            foreach (LocationLink link in LocationLinks)
            {
                if (Locations.TryGetValue(link.A, out Location A))
                {
                    A.LocationLinksA.Add(link);
                }

                if (Locations.TryGetValue(link.B, out Location B))
                {
                    B.LocationLinksB.Add(link);
                }
            }
        }

        [DbFunction("ConnectomeModel.Store", "ufnStructureArea")]
        public string GetStructureArea(long ID)
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            List<ObjectParameter> parameters =
            [
                new("Id", ID)
            ];

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.ufnStructureArea(@Id)", [.. parameters])
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "ufnStructureVolume")]
        public string GetStructureVolume(long ID)
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            List<ObjectParameter> parameters =
            [
                new("Id", ID)
            ];

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.ufnStructureVolume(@Id)", [.. parameters])
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "XYScale")]
        public double GetXYScale()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            List<ObjectParameter> parameters = [];

            return objectContext.CreateQuery<double>("ConnectomeModel.Store.XYScale()", [.. parameters])
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "ZScale")]
        public double GetZScale()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            List<ObjectParameter> parameters = [];

            return objectContext.CreateQuery<double>("ConnectomeModel.Store.ZScale()", [.. parameters])
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "XYScaleUnits")]
        public string GetXYUnits()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            List<ObjectParameter> parameters = [];

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.XYScaleUnits()", [.. parameters])
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "ZScaleUnits")]
        public string GetZUnits()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            List<ObjectParameter> parameters = [];

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.ZScaleUnits()", [.. parameters])
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }
    }
}
