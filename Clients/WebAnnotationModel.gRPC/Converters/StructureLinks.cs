using System;
using System.ComponentModel;
using WebAnnotationModel.gRPC.Converters; 
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace WebAnnotationModel.gRPC.Converters
{

    public class StructureLinkServerToClientConverter : IObjectConverter<StructureLink, StructureLinkObj>,
        IObjectConverter<IStructureLink, StructureLinkObj>
    {
        public StructureLinkObj Convert(StructureLink src)
        {
            StructureLinkObj obj =
                new StructureLinkObj(src.SourceId, src.TargetId, src.Bidirectional);
            return obj;
        }

        public StructureLinkObj Convert(IStructureLink src)
        {
            if (src is StructureLink concrete)
                return Convert(concrete);

            return new StructureLinkObj((long)src.SourceID, (long)src.TargetID, !src.Directional);
        }
    }

    public class StructureLinkClientToServerConverter : IObjectConverter<StructureLinkObj, StructureLink>
    {
        public StructureLink Convert(StructureLinkObj src)
        { 
            StructureLink obj =
                new StructureLink
                {
                    SourceId = src.SourceID,
                    TargetId = src.TargetID,
                    Bidirectional = src.Bidirectional
                };
            return obj;
        }
    }

    public class StructureLinkServerToClientUpdater : IObjectUpdater<StructureLinkObj, StructureLink>
    {
        public Task<bool> Update(StructureLinkObj obj, StructureLink update)
        {
            //Structure links currently have no properties that can be updated so we always return false
            return Task.FromResult(false);
            /*

            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged; //Record change events so we know if an update occurred.

                obj.Bidirectional = update.Bidirectional; 
            }
            finally
            {
                obj.PropertyChanged -= OnPropertyChanged;
            }

            return updated;
            */
        }
    }
}