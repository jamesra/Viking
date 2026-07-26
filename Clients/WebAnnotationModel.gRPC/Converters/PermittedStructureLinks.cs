using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Microsoft.Extensions.DependencyInjection;

namespace WebAnnotationModel.gRPC.Converters
{
 
    public class PermittedStructureLinkServerToClientConverter : IObjectConverter<PermittedStructureLink, PermittedStructureLinkObj>
    {
        public PermittedStructureLinkObj Convert(PermittedStructureLink src)
        {
            PermittedStructureLinkObj obj =
                new PermittedStructureLinkObj(src.SourceTypeId, src.TargetTypeId, src.Bidirectional);
            return obj;
        }
    }

    public class PermittedStructureLinkClientToServerConverter : IObjectConverter<PermittedStructureLinkObj, PermittedStructureLink>
    {
        public PermittedStructureLink Convert(PermittedStructureLinkObj src)
        {

            PermittedStructureLink obj =
                new PermittedStructureLink
                {
                    SourceTypeId = src.SourceTypeID,
                    TargetTypeId = src.TargetTypeID,
                    Bidirectional = src.Bidirectional
                };
            return obj;
        }
    }

    public class PermittedStructureLinkServerToClientUpdater : IObjectUpdater<PermittedStructureLinkObj, PermittedStructureLink>
    {
        public Task<bool> Update(PermittedStructureLinkObj obj, PermittedStructureLink update)
        {
            throw new NotImplementedException();
        }
    }
}
