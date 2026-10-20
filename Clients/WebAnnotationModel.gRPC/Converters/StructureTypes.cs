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
    /// <summary>
    /// Builds a new <see cref="StructureTypeObj"/> for a type the client sees for the first time (the GetAll table
    /// load on a cache miss, and the server's echo of a type created through <c>StructureTypeStore.Create</c>).
    /// </summary>
    /// <remarks>
    /// The result mirrors the server, so its DBAction must end as <see cref="DBACTION.NONE"/>: <c>Add</c> queues any
    /// object whose DBAction is not NONE for the next Save. <c>DBAction</c> is assigned last in each initializer
    /// because the Name, Notes and ParentID setters mark the object UPDATE.
    /// </remarks>
    public class StructureTypeServerToClientConverter : IObjectConverter<StructureType, StructureTypeObj>,
        IObjectConverter<IStructureType, StructureTypeObj>
    {
        /// <summary>Converts a proto structure type, copying every field the proto carries, including the optional parent id.</summary>
        public StructureTypeObj Convert(StructureType src)
        {
            StructureTypeObj obj =
                new StructureTypeObj(src.Id)
                {
                    Code = src.Code,
                    Color = (uint)src.Color,
                    Name = src.Name,
                    Notes = src.Notes,
                    ParentID = src.HasParentId ? (long?)src.ParentId : (long?)null,
                    DBAction = DBACTION.NONE,
                };

            ApplyAttributes(obj, src.Attributes);

            return obj;
        }

        /// <summary>
        /// Converts any <see cref="IStructureType"/>; a proto <see cref="StructureType"/> takes the full proto path.
        /// </summary>
        public StructureTypeObj Convert(IStructureType src)
        {
            if (src is StructureType concrete)
                return Convert(concrete);

            StructureTypeObj obj =
                new StructureTypeObj((long)src.ID)
                {
                    Code = src.Code,
                    Color = src.Color,
                    Name = src.Name,
                    Notes = src.Notes,
                    ParentID = src.ParentID,
                    DBAction = DBACTION.NONE,
                };

            ApplyAttributes(obj, src.Attributes);

            return obj;
        }

        private static void ApplyAttributes(StructureTypeObj obj, string attributes)
        {
            try
            {
                obj.SetAttributes(ObjAttributeParser.ParseAttributes(attributes ?? string.Empty)).Wait();
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Ignoring unparsable attributes on structure type {obj.ID}: {e.Message}",
                    "WebAnnotation");
            }
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
                    Name = src.Name ?? string.Empty,
                    Notes = src.Notes ?? string.Empty,
                    Code = src.Code ?? string.Empty,
                    Color = src.Color,
                    Attributes = src.Attributes?.ToXml() ?? string.Empty,
                };

            if (src.ParentID.HasValue)
                obj.ParentId = src.ParentID.Value;

            ((IChangeAction)obj).DBAction = src.DBAction;

            return obj;
        }

        IStructureType IObjectConverter<StructureTypeObj, IStructureType>.Convert(StructureTypeObj src)
        {
            return (IStructureType)Convert(src);
        }
    }

    /// <summary>
    /// Copies a server refresh onto a cached structure type without marking it for Save. A type with an unsaved local
    /// edit (DBAction not NONE) keeps the user's values until that edit is saved.
    /// </summary>
    public class StructureTypeServerToClientUpdater : IObjectUpdater<StructureTypeObj, StructureType>
    {
        public async Task<bool> Update(StructureTypeObj obj, StructureType update)
        {
            if (obj.DBAction != DBACTION.NONE)
                return false;

            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged; //Record change events so we know if an update occurred.

                obj.ApplyServerValues(() =>
                {
                    obj.Code = update.Code;
                    obj.Color = update.Color;
                    obj.Name = update.Name;
                    obj.Notes = update.Notes;
                    obj.ParentID = update.HasParentId ? (long?)update.ParentId : (long?)null;
                });
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
