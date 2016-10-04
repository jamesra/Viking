
using System;
using System.Data.Entity;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using EntityFrameworkExtras;

namespace ConnectomeDataModel
{
    public class NetworkDetails
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

    public struct AnnotationCollection
    {
        public readonly IDictionary<long, Structure> Structures;
        public readonly IDictionary<long, Location> Locations;

        public AnnotationCollection(IDictionary<long, Structure> structs, IDictionary<long, Location>  locs)
        {
            this.Structures = structs;
            this.Locations = locs;
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

            NetworkDetails retval = null;

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
            SortedSet<long> NodeIDs = SelectNetworkStructureIDs(IDs, numHops);

            return from s in this.Structures
                   where NodeIDs.Contains(s.ID)
                   select s; 
        }

        public IQueryable<Structure> SelectNetworkChildStructures(IEnumerable<long> IDs, int numHops)
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

        public IQueryable<StructureLink> SelectNetworkStructureLinks(IEnumerable<long> IDs, int numHops)
        {
            SortedSet<long> NodeIDs = SelectNetworkStructureIDs(IDs, numHops);
            
            var ChildStructures = from S in Structures where S.ParentID.HasValue && NodeIDs.Contains(S.ParentID.Value) select S;
            return from SL in this.StructureLinks
                    join CSource in ChildStructures on SL.SourceID equals CSource.ID
                    join CTarget in ChildStructures on SL.TargetID equals CTarget.ID
                    select SL;
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
    }
}
