using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace ConnectomeDataModel
{
    public partial class ConnectomeEntities
    {
        public IQueryable<Location> SelectResidualFieldCandidateLocations(int minLocations)
        {
            int min = minLocations < 1 ? 3 : minLocations;
            if (Database.Connection.State != ConnectionState.Open)
                Database.Connection.Open();

            Location[] rows;
            using (var cmd = Database.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM dbo.ResidualFieldCandidateLocations(@MinLocations)";
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = "@MinLocations";
                parameter.Value = min;
                cmd.Parameters.Add(parameter);
                using var reader = cmd.ExecuteReader();
                rows = [.. ((IObjectContextAdapter)this).ObjectContext.Translate<Location>(reader, "Locations", MergeOption.NoTracking)];
            }

            Database.Connection.Close();
            return rows.AsQueryable();
        }
    }
}
