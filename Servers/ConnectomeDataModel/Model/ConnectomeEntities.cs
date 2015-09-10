
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


        public IList<Location> spSectionLocationsAndLinks(long section, DateTime? LastModified)
        {
            DbCommand sp = this.Database.Connection.CreateCommand();
            sp.CommandText = "[dbo].[SelectSectionLocationsAndLinks] @Z, @QueryDate";
            sp.Parameters.Add(CreateSectionNumberParameter(section));
            sp.Parameters.Add(CreateDateTimeParameter(LastModified));

            this.Database.Connection.Open();

            DbDataReader reader = sp.ExecuteReader(); 
            Dictionary<long, Location> dictLocations = ((IObjectContextAdapter)this).ObjectContext.Translate<Location>(reader, "Locations", MergeOption.NoTracking).ToDictionary(l => l.ID);

            reader.NextResult();

            var LocationLinks = ((IObjectContextAdapter)this).ObjectContext.Translate<LocationLink>(reader, "LocationLinks", MergeOption.NoTracking);

            AppendLinksToLocations(dictLocations, LocationLinks.ToList());

            return dictLocations.Values.ToList();
        }


        public IQueryable<Location> ReadSectionLocations(long section, DateTime? LastModified)
        {
            if(LastModified.HasValue)
            {
                return this.SectionLocationsModifiedAfterDate((double)section, LastModified);
            }
            else
            {
                return this.SectionLocations((double)section);
            }
            
        }

        public IQueryable<LocationLink> ReadSectionLocationLinks(long section, DateTime? LastModified)
        {
            if (LastModified.HasValue)
            {
                return this.SectionLocationLinksModifiedAfterDate((double)section, LastModified);
            }
            else
            {
                return this.SectionLocationLinks((double)section);
            }
        }


        public IList<Location> ReadSectionLocationsAndLinks(long section, DateTime? LastModified)
        {
            IQueryable<Location> Locations = null;
            ObjectResult<LocationLink> LocationLinks = this.SelectSectionLocationLinks((double)section, LastModified);

            if (LastModified.HasValue)
            {
                Locations = (from l in this.SectionLocations((double)section) where l.LastModified >= LastModified.Value select l).Include("LocationLinksA,LocationLinksB"); //this.Locations.Where(l => l.Z == (double)section && l.LastModified >= LastModified.Value);
            }
            else
            {
                Locations = (from l in this.SectionLocations((double)section) select l).Include("LocationLinksA,LocationLinksB");
            }
            
            return Locations.ToList();
        }

        public IList<Location> ReadSectionLocationsAndLinksInBounds(long section, System.Data.Entity.Spatial.DbGeometry bbox, DateTime? LastModified)
        {
            IQueryable<Location> Locations = null;
            
            if (LastModified.HasValue)
            {
                Locations = (from l in this.BoundedLocations(bbox) where l.Z == (double)section && l.LastModified >= LastModified.Value select l).Include("LocationLinksA,LocationLinksB"); //this.Locations.Where(l => l.Z == (double)section && l.LastModified >= LastModified.Value);
            }
            else
            {
                Locations = (from l in this.BoundedLocations(bbox) where l.Z == (double)section select l).Include("LocationLinksA,LocationLinksB");
            }

            return Locations.ToList();
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

        public IList<Structure> ReadSectionStructuresAndLinks(long section, DateTime? LastModified)
        {
            DbCommand sp = this.Database.Connection.CreateCommand();
            sp.CommandText = "[dbo].[SelectStructuresForSection] @Z, @QueryDate";
            sp.Parameters.Add(CreateSectionNumberParameter(section));
            sp.Parameters.Add(CreateDateTimeParameter(LastModified));

            this.Database.Connection.Open();

            DbDataReader reader = sp.ExecuteReader();
            Dictionary<long, Structure> dictStructures = ((IObjectContextAdapter)this).ObjectContext.Translate<Structure>(reader, "Structures", MergeOption.NoTracking).ToDictionary(s => s.ID);

            reader.NextResult();

            var StructureLinks = ((IObjectContextAdapter)this).ObjectContext.Translate<StructureLink>(reader, "StructureLinks", MergeOption.NoTracking);

            AppendLinksToStructures(dictStructures, StructureLinks.ToList());

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
    }
}
