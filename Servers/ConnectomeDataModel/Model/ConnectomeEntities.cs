
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
    public readonly struct NetworkDetails
    {
        public readonly Structure[] Nodes;
        public readonly Structure[] ChildNodes;
        public readonly StructureLink[] Edges;

        public NetworkDetails(Structure[] nodes, Structure[] childNodes, StructureLink[] edges)
        {
            this.Nodes = nodes;
            this.ChildNodes = childNodes;
            this.Edges = edges;
        } 
    }

    public readonly struct AnnotationCollection
    {
        public readonly IDictionary<long, Structure> Structures;
        public readonly IDictionary<long, Location> Locations;

        public AnnotationCollection(IDictionary<long, Structure> structs, IDictionary<long, Location>  locs)
        {
            this.Structures = structs;
            this.Locations = locs;
        }
    }

    /// <summary>
    /// User to hold data from a DBReader as SQLGeometry objects are converted on other threads
    /// </summary>
    public class UnconvertedStructureSpatialCache
    {
        public StructureSpatialCache row = new StructureSpatialCache();
        public System.Threading.Tasks.Task<System.Data.Entity.Spatial.DbGeometry> ConvexHullTask = null;
        public System.Threading.Tasks.Task<System.Data.Entity.Spatial.DbGeometry> BBoxTask = null;

        private static System.Data.Entity.Spatial.DbGeometry UnpackSqlGeometry(Microsoft.SqlServer.Types.SqlGeometry input)
        {
            //return System.Data.Entity.Spatial.DbGeometry.FromBinary(input.STAsBinary().Buffer);
            return System.Data.Entity.Spatial.DbGeometry.FromText(input.ToString());
        }
          
        public static UnconvertedStructureSpatialCache PopulateAsync(System.Data.Common.DbDataReader reader)
        {
            UnconvertedStructureSpatialCache obj = new UnconvertedStructureSpatialCache();
            obj.row.ID = reader.GetInt64(0);
            //row.BoundingRect = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1).STAsBinary().Buffer);
            //obj.row.BoundingRect = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1).STAsBinary().Buffer);
            Microsoft.SqlServer.Types.SqlGeometry bbox_input = reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(1);
            obj.BBoxTask = Task<System.Data.Entity.Spatial.DbGeometry>.Run(() => { return UnpackSqlGeometry(bbox_input); });
            obj.row.Area = reader.GetDouble(2);
            obj.row.Volume = reader.GetDouble(3);
            obj.row.MaxDimension = reader.GetInt32(4);
            obj.row.MinZ = reader.GetDouble(5);
            obj.row.MaxZ = reader.GetDouble(6);
            //row.ConvexHull = System.Data.Entity.Spatial.DbGeometry.FromBinary(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7).STAsBinary().Buffer);
            //row.ConvexHull = System.Data.Entity.Spatial.DbGeometry.FromText(reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7).ToString());
            Microsoft.SqlServer.Types.SqlGeometry convex_hull_input = reader.GetFieldValue<Microsoft.SqlServer.Types.SqlGeometry>(7);
            obj.ConvexHullTask = Task<System.Data.Entity.Spatial.DbGeometry>.Run(() => { return UnpackSqlGeometry(convex_hull_input); });
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
            SqlParameter param = new SqlParameter("Z", System.Data.SqlDbType.Float);
            param.Direction = System.Data.ParameterDirection.Input;
            param.SqlValue = new System.Data.SqlTypes.SqlDouble((double)section);

            return param;
        }



        private static SqlParameter CreateMinRadiusParameter(double MinRadius)
        {
            SqlParameter param = new SqlParameter("MinRadius", System.Data.SqlDbType.Float);
            param.Direction = System.Data.ParameterDirection.Input;
            param.SqlValue = new System.Data.SqlTypes.SqlDouble((double)MinRadius);

            return param;
        }

        private static SqlParameter CreateBoundingBoxParameter(System.Data.Entity.Spatial.DbGeometry bbox)
        {
            
            System.Data.SqlDbType dbGeoType = System.Data.SqlDbType.Udt;
            SqlParameter param = new SqlParameter("BBox", dbGeoType);
            param.UdtTypeName = "geometry";
            param.Direction = System.Data.ParameterDirection.Input;
            param.SqlValue = bbox;

            return param;
        }

        private static SqlParameter CreateDateTimeParameter(DateTime? time)
        {
            SqlParameter param = new SqlParameter("QueryDate", System.Data.SqlDbType.DateTime);
            param.Direction = System.Data.ParameterDirection.Input;

            if (!time.HasValue)
                param.SqlValue = DBNull.Value;
            else
                param.SqlValue = new System.Data.SqlTypes.SqlDateTime(time.Value);
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
                return this.SectionLocations((double)section).Where(l=> l.LastModified >= LastModified.Value);
            }
            else
            {
                return this.SectionLocations((double)section);
            }
        } 

        public IList<Location> ReadSectionLocationsAndLinks(long section, DateTime? LastModified)
        {
            var results = this.SelectSectionLocationsAndLinks((double)section, LastModified, MergeOption.NoTracking);

            var dictLocations = results.ToDictionary(l => l.ID);

            var LocationLinks = results.GetNextResult<LocationLink>().ToList();
            
            AppendLinksToLocations(dictLocations, LocationLinks);

            return dictLocations.Values.ToList();
        }

        public IList<Location> ReadSectionLocationsAndLinksInMosaicRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionLocationsAndLinksInMosaicBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            var dictLocations = results.ToDictionary(l => l.ID);

            var LocationLinks = results.GetNextResult<LocationLink>().ToList();

            AppendLinksToLocations(dictLocations, LocationLinks);

            return dictLocations.Values.ToList();
        }

        public IList<Location> ReadSectionLocationsAndLinksInVolumeRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionLocationsAndLinksInVolumeBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            var dictLocations = results.ToDictionary(l => l.ID);

            var LocationLinks = results.GetNextResult<LocationLink>().ToList();

            AppendLinksToLocations(dictLocations, LocationLinks);

            return dictLocations.Values.ToList();
        }

        public IList<Location> ReadStructureLocationsAndLinks(long StructureID)
        {
            var results = this.SelectStructureLocationsAndLinks(StructureID);

            var dictLocations = results.ToDictionary(l => l.ID);
            var LocationLinks = results.GetNextResult<LocationLink>().ToList();
            
            AppendLinksToLocations(dictLocations, LocationLinks);

            return dictLocations.Values.ToList();
        }

        public IList<Structure> ReadSectionStructuresAndLinks(long section, DateTime? LastModified)
        {
            var results = this.SelectSectionStructuresAndLinks((double)section, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>().ToList();
             
            AppendLinksToStructures(dictStructures, StructureLinks);

            return dictStructures.Values.ToList();
        }

        public IList<Structure> ReadSectionStructuresAndLinksInMosaicRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionStructuresAndLinksInMosaicBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>().ToList();

            AppendLinksToStructures(dictStructures, StructureLinks);

            return dictStructures.Values.ToList();
        }

        public IList<Structure> ReadSectionStructuresAndLinksInVolumeRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionStructuresAndLinksInVolumeBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>().ToList();

            AppendLinksToStructures(dictStructures, StructureLinks);

            return dictStructures.Values.ToList();
        }


        public AnnotationCollection ReadSectionAnnotationsInMosaicRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionAnnotationsInMosaicBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>();

            AppendLinksToStructures(dictStructures, StructureLinks.ToList());

            var Locations = StructureLinks.GetNextResult<Location>();

            Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);

            var LocationLinks = Locations.GetNextResult<LocationLink>();

            AppendLinksToLocations(dictLocations, LocationLinks.ToList());

            return new AnnotationCollection(dictStructures, dictLocations);
        }

        public AnnotationCollection ReadSectionAnnotationsInVolumeRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionAnnotationsInVolumeBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>();

            AppendLinksToStructures(dictStructures, StructureLinks.ToList());

            var Locations = StructureLinks.GetNextResult<Location>();

            Dictionary<long, Location> dictLocations = Locations.ToDictionary(l => l.ID);

            var LocationLinks = Locations.GetNextResult<LocationLink>();

            AppendLinksToLocations(dictLocations, LocationLinks.ToList());

            return new AnnotationCollection(dictStructures, dictLocations);
        }

        public virtual int SplitStructure(long keepStructureID, long firstLocationIDOfSplitStructure, out long NewStructureID)
        {
            var keepStructureIDParameter = new ObjectParameter("KeepStructureID", keepStructureID);
            var firstLocationIDOfSplitStructureParameter = new ObjectParameter("FirstLocationIDOfSplitStructure", firstLocationIDOfSplitStructure);
            var NewStructureIDParam = new ObjectParameter("SplitStructureID", typeof(long));

            int retval = ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("SplitStructure", keepStructureIDParameter, firstLocationIDOfSplitStructureParameter, NewStructureIDParam);
            if (retval != 0)
                NewStructureID = -1;
            else
                NewStructureID = (long)NewStructureIDParam.Value;

            return retval;
        }

        public virtual int SplitStructureAtLocationLink(long LocationIDOfKeepStructure, long LocationIDOfSplitStructure, out long NewStructureID)
        {
            var LocationIDOfKeepStructureParameter = new ObjectParameter("LocationIDOfKeepStructure", LocationIDOfKeepStructure);
            var LocationIDOfSplitStructureParameter = new ObjectParameter("LocationIDOfSplitStructure", LocationIDOfSplitStructure);
            var NewStructureIDParam = new ObjectParameter("SplitStructureID", typeof(long));

            int retval = ((IObjectContextAdapter)this).ObjectContext.ExecuteFunction("SplitStructureAtLocationLink", LocationIDOfKeepStructureParameter, LocationIDOfSplitStructureParameter, NewStructureIDParam);
            if (retval != 0)
                NewStructureID = -1;
            else
                NewStructureID = (long)NewStructureIDParam.Value;

            return retval;
        }


        public SortedSet<long> SelectNetworkStructureIDs(IEnumerable<long> IDs, int numHops)
        {
            var proc = new SelectNetworkStructureIDsStoredProcedure()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            SortedSet<long> StructureIDs = new SortedSet<long>(EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteStoredProcedure<long>(this.Database, proc));
            return StructureIDs;
        }

        public NetworkDetails SelectNetworkDetails(IEnumerable<long> IDs, int numHops)
        { 
            var proc = new SelectNetworkDetailsStoredProcedure()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            NetworkDetails retval;

            if(this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                Structure[] NodeObjects = ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking).ToArray();
                reader.NextResult();
                Structure[] ChildObjects = ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking).ToArray();
                reader.NextResult();
                StructureLink[] Edges = ((IObjectContextAdapter)this).ObjectContext.Translate<StructureLink>(reader, "StructureLinks", MergeOption.NoTracking).ToArray();

                retval = new NetworkDetails(NodeObjects, ChildObjects, Edges);
            }

            this.Database.Connection.Close();

            return retval;
        }

        public IQueryable<Structure> SelectNetworkStructures(IEnumerable<long> IDs, int numHops)
        {
            var proc = new SelectNetworkStructuresProcedure()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };
              
            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            Structure[] NodeObjects; 
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                NodeObjects = ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking).ToArray();
            }

            this.Database.Connection.Close();

            return NodeObjects.AsQueryable<Structure>();
        }

        public IQueryable<Structure> SelectNetworkChildStructuresIDs(IEnumerable<long> IDs, int numHops)
        {
            var proc = new SelectNetworkChildStructureIDsProcedure()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };
            
            SortedSet<long> ChildStructureIDs = new SortedSet<long>(EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteStoredProcedure<long>(this.Database, proc));
            
            return from s in this.Structures
                   where ChildStructureIDs.Contains(s.ID)
                   select s;
        }

        public IQueryable<StructureSpatialCache> SelectNetworkStructureSpatialData(IEnumerable<long> IDs, int numHops)
        {
            var proc = new SelectNetworkStructureSpatialData()
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
            var proc = new SelectNetworkChildStructureSpatialData()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            List<StructureSpatialCache> NodeObjects = new List<StructureSpatialCache>();
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
            List<UnconvertedStructureSpatialCache> NodeObjects = new List<UnconvertedStructureSpatialCache>();

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

            return NodeObjects.Select(o => o.WaitReturn()).ToList();
        }

        public IQueryable<Structure> SelectNetworkChildStructures(IEnumerable<long> IDs, int numHops)
        {
            var proc = new SelectNetworkChildStructuresProcedure()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            Structure[] ChildStructures;
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                ChildStructures = ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking).ToArray();
            }

            this.Database.Connection.Close();

            return ChildStructures.AsQueryable<Structure>();
        }

        public IQueryable<StructureLink> SelectNetworkStructureLinks(IEnumerable<long> IDs, int numHops)
        {
            var proc = new SelectNetworkStructureLinksProcedure()
            {
                Hops = numHops,
                IDs = udt_integer_list.Create(IDs)
            };

            if (this.Database.Connection.State != System.Data.ConnectionState.Open)
                this.Database.Connection.Open();

            StructureLink[] Links;
            using (System.Data.Common.DbDataReader reader = EntityFrameworkExtras.EF6.DatabaseExtensions.ExecuteReader(this.Database, proc))
            {
                Links = ((IObjectContextAdapter)this).ObjectContext.Translate<StructureLink>(reader, "StructureLinks", MergeOption.NoTracking).ToArray();
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
            Structure Source;
            Structure Target;
            foreach (StructureLink link in StructureLinks)
            {
                if (Structures.TryGetValue(link.SourceID, out Source))
                {
                    Source.SourceOfLinks.Add(link);
                }

                if (Structures.TryGetValue(link.TargetID, out Target))
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
            Location A;
            Location B;
            foreach (LocationLink link in LocationLinks)
            {
                if (Locations.TryGetValue(link.A, out A))
                {
                    A.LocationLinksA.Add(link);
                }

                if (Locations.TryGetValue(link.B, out B))
                {
                    B.LocationLinksB.Add(link);
                }
            } 
        }
         
        [DbFunction("ConnectomeModel.Store", "ufnStructureArea")]
        public string GetStructureArea(long ID)
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            var parameters = new List<ObjectParameter>();
            parameters.Add(new ObjectParameter("Id", ID));

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.ufnStructureArea(@Id)", parameters.ToArray())
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "ufnStructureVolume")]
        public string GetStructureVolume(long ID)
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            var parameters = new List<ObjectParameter>();
            parameters.Add(new ObjectParameter("Id", ID));

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.ufnStructureVolume(@Id)", parameters.ToArray())
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "XYScale")]
        public double GetXYScale()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            var parameters = new List<ObjectParameter>(); 
            
            return objectContext.CreateQuery<double>("ConnectomeModel.Store.XYScale()", parameters.ToArray())
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "ZScale")]
        public double GetZScale()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            var parameters = new List<ObjectParameter>();

            return objectContext.CreateQuery<double>("ConnectomeModel.Store.ZScale()", parameters.ToArray())
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "XYScaleUnits")]
        public string GetXYUnits()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            var parameters = new List<ObjectParameter>();

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.XYScaleUnits()", parameters.ToArray())
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }

        [DbFunction("ConnectomeModel.Store", "ZScaleUnits")]
        public string GetZUnits()
        {
            var objectContext = ((IObjectContextAdapter)this).ObjectContext;

            var parameters = new List<ObjectParameter>();

            return objectContext.CreateQuery<string>("ConnectomeModel.Store.ZScaleUnits()", parameters.ToArray())
                 .Execute(MergeOption.NoTracking)
                 .FirstOrDefault();
        }
    }
}
