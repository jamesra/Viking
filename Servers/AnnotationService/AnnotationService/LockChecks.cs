using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConnectomeDataModel;

namespace Annotation
{
    public static class LockChecks
    {
        public static bool AreAnyLocationsLocked(ConnectomeEntities db, long[] locationIDs)
        {
            IQueryable<ConnectomeDataModel.Structure> UniqueStructures =
                   (from l in db.Locations
                    join s in db.Structures
                    on l.ParentID equals s.ID
                    where locationIDs.Contains(l.ID)
                    select s).Distinct();

            return UniqueStructures.AreAnyStructuresLocked(db);
        }

        public static bool AreAnyStructuresLocked(this IQueryable<ConnectomeDataModel.Structure> UniqueStructures, ConnectomeEntities db)
        {
            if (!UniqueStructures.Any())
                return false;

            if (UniqueStructures.Any(s => s.Verified))
                return true; 

            //Check parents for verified
            IQueryable<ConnectomeDataModel.Structure> ParentStructures =
                (from s in UniqueStructures
                 join p in db.Structures
                 on s.ParentID equals p.ID
                 where s.ParentID.HasValue
                 select s).Distinct();
            
            return ParentStructures.AreAnyStructuresLocked(db);
        }

        public static bool AreAnyStructuresLocked(ConnectomeEntities db, long[] structureIDs)
        {
            IQueryable<ConnectomeDataModel.Structure> UniqueStructures =
                from s in db.Structures
                where structureIDs.Contains(s.ID)
                select s;

            return UniqueStructures.AreAnyStructuresLocked(db);
        }
    }
}
