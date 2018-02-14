using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkExtras;
using EntityFrameworkExtras.EF6;

namespace ConnectomeDataModel
{
    class NetworkQueryParameters
    {
        [StoredProcedureParameter(System.Data.SqlDbType.Udt, Direction = System.Data.ParameterDirection.Input)]
        public List<udt_integer_list> IDs { get; set; }

        [StoredProcedureParameter(System.Data.SqlDbType.Int, Direction = System.Data.ParameterDirection.Input)]
        public int Hops { get; set; }
    }
    
    [StoredProcedure("SelectNetworkDetails")]
    class SelectNetworkDetailsStoredProcedure : NetworkQueryParameters
    {
    
    }

    [StoredProcedure("SelectNetworkStructures")]
    class SelectNetworkStructuresProcedure : NetworkQueryParameters
    {
   
    }

    [StoredProcedure("SelectNetworkStructureLinks")]
    class SelectNetworkStructureLinksProcedure : NetworkQueryParameters
    {
        
    }

    [StoredProcedure("SelectNetworkChildStructures")]
    class SelectNetworkChildStructuresProcedure : NetworkQueryParameters
    {

    }


    [StoredProcedure("SelectNetworkStructureIDs")]
    class SelectNetworkStructureIDsStoredProcedure : NetworkQueryParameters
    {

    }

    [StoredProcedure("SelectNetworkChildStructureIDs")]
    class SelectNetworkChildStructureIDsProcedure : NetworkQueryParameters
    {
       
    }

    [StoredProcedure("SelectNetworkStructureSpatialData")]
    class SelectNetworkStructureSpatialData : NetworkQueryParameters
    {

    }

    [StoredProcedure("SelectNetworkChildStructureSpatialData")]
    class SelectNetworkChildStructureSpatialData : NetworkQueryParameters
    {

    }

    [UserDefinedTableType("integer_list")]
    struct udt_integer_list
    {
        [UserDefinedTableTypeColumn(0, "ID")]
        public long ID { get; set; }

        public udt_integer_list(long id)
        {
            this.ID = id;
        }

        public static List<udt_integer_list> Create(IEnumerable<long> ids)
        {
            return ids.Select(id => new udt_integer_list(id)).ToList();
        }
    }
}
