using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using System.ComponentModel;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;

namespace WebAnnotationModel.gRPC.Converters
{
    public static class StructureTypeConverterExtensions
    {
        public static IServiceCollection AddStandardStructureTypeConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<StructureType, StructureTypeObj>, StructureTypeServerToClientConverter>();
            service.AddSingleton<IObjectConverter<StructureTypeObj, StructureType>, StructureTypeClientToServerConverter>();
            service.AddTransient<IObjectUpdater<StructureTypeObj, StructureType>, StructureTypeServerToClientUpdater>();
            return service;
        }
    }

    public class StructureTypeServerToClientConverter : IObjectConverter<StructureType, StructureTypeObj>
    {
        public StructureTypeObj Convert(StructureType src)
        {
            StructureTypeObj obj =
                new StructureTypeObj(src.Id)
                {
                    Code = src.Code,
                    Color = (uint)src.Color,
                    DBAction = DBACTION.NONE,
                    Name = src.Name,
                    Notes = src.Notes,
                    ParentID = src.ParentId,
                };

            obj.SetAttributes(src.Attributes.ParseAttributes()).Wait();

            return obj;
        }
    }

    public class StructureTypeClientToServerConverter : IObjectConverter<StructureTypeObj, StructureType>,
        IObjectConverter<StructureTypeObj, IStructureType>
    {
        public StructureType Convert(StructureTypeObj src)
        {
            StructureType obj =
                new StructureType
                {
                    Id = src.ID,
                    Name = src.Name,
                    Notes = src.Notes,
                    Code = src.Code,
                    Color = src.Color,
                };

            if (src.ParentID.HasValue)
                obj.ParentId = src.ParentID.Value;

            obj.Attributes = src.Attributes.ToXml();

            return obj;
        }

        IStructureType IObjectConverter<StructureTypeObj, IStructureType>.Convert(StructureTypeObj src)
        {
            return (IStructureType)Convert(src);
        }
    }

    public class StructureTypeServerToClientUpdater : IObjectUpdater<StructureTypeObj, StructureType>
    {
        public async Task<bool> Update(StructureTypeObj obj, StructureType update)
        {
            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged; //Record change events so we know if an update occurred.

                obj.Code = update.Code;
                obj.Color = update.Color;
                obj.Name = update.Name;
                obj.Notes = update.Notes;
                obj.ParentID = update.ParentId;
                await obj.SetAttributes(update.Attributes.ParseAttributes());
            }
            finally
            {
                obj.PropertyChanged -= OnPropertyChanged;
            }

            return updated;
        } 
    }
}
