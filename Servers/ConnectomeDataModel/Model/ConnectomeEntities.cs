
using System;
using System.Data.Entity;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace ConnectomeDataModel
{
    public partial class ConnectomeEntities
    {
        /// <summary>
        /// Our server didn't exist before 2007 and if we pass a date earlier than 1753 the SQL query fails
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static DateTime? ValidateDate(DateTime? input)
        {
            if (input.HasValue == false)
                return input;

            if (input < new DateTime(2007, 1, 1))
                return new DateTime(2007, 1, 1);
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

        public IList<Location> ReadSectionLocationsAndLinksInRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionLocationsAndLinksInBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

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

        public IList<Structure> ReadSectionStructuresAndLinksInRegion(long section, System.Data.Entity.Spatial.DbGeometry bbox, double MinRadius, DateTime? LastModified)
        {
            var results = this.SelectSectionStructuresAndLinksInBounds((double)section, bbox, MinRadius, LastModified, MergeOption.NoTracking);

            Dictionary<long, Structure> dictStructures = results.ToDictionary(s => s.ID);

            var StructureLinks = results.GetNextResult<StructureLink>().ToList();

            AppendLinksToStructures(dictStructures, StructureLinks);

            return dictStructures.Values.ToList();
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
    }
}
