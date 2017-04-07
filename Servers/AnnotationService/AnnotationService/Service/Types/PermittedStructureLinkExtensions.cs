using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationService.Types
{
    public static class PermittedStructureLinkExtensions
    {
        public static PermittedStructureLink Create(this ConnectomeDataModel.PermittedStructureLink obj)
        {
            PermittedStructureLink psl = new PermittedStructureLink();
            ConnectomeDataModel.PermittedStructureLink db = obj;

            psl.SourceTypeID = db.SourceTypeID;
            psl.TargetTypeID = db.TargetTypeID;
            psl.Bidirectional = db.Bidirectional;
            return psl;
        }

        public static void Sync(this PermittedStructureLink psl, ConnectomeDataModel.PermittedStructureLink db)
        {
            db.SourceTypeID = psl.SourceTypeID;
            db.TargetTypeID = psl.TargetTypeID;
            db.Bidirectional = psl.Bidirectional;
        }
    }
}
