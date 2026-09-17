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
using WebAnnotationModel;

namespace WebAnnotationModel.gRPC.Converters
{

    public class StructureServerToClientConverter : IObjectConverter<Structure, StructureObj>,
        IObjectConverter<IStructure, StructureObj>
    {
        public StructureObj Convert(Structure src)
        {
            StructureObj obj =
                new StructureObj(src.Id, src.TypeId)
                {
                    DBAction = DBACTION.NONE,
                    Label = src.Label,
                    Notes = src.Notes,
                    Confidence = src.Confidence, 
                    Username = src.Username, 
                    Verified = src.Verified,
                    LastModified = src.LastModified?.ToDateTime() ?? default,
                    Created = src.Created?.ToDateTime() ?? default,
                };

            obj.SetAttributes(ObjAttributeParser.ParseAttributes(src.Attributes ?? string.Empty)).Wait();
            // Type stays null until StructureTypes is initialized; views that assume Type must wait for GetAll.
            if (Store.IsInitialized)
            {
                Store.StructureTypes.TryGetObjectByID(src.TypeId, out var type);
                obj.Type = type;
            }
            return obj;
        }

        public StructureObj Convert(IStructure src)
        {
            if (src is Structure concrete)
                return Convert(concrete);

            StructureObj obj =
                new StructureObj((long)src.ID, src.TypeID)
                {
                    DBAction = DBACTION.NONE,
                    Label = src.Label,
                };

            obj.SetAttributes(ObjAttributeParser.ParseAttributes(src.Attributes ?? string.Empty)).Wait();
            if (Store.IsInitialized)
            {
                Store.StructureTypes.TryGetObjectByID(src.TypeID, out var type);
                obj.Type = type;
            }
            return obj;
        }
    }

    public class  StructureClientToServerConverter : IObjectConverter<StructureObj, Structure>,
        IObjectConverter<StructureObj, IStructure>
    {
        public Structure Convert(StructureObj src)
        { 
            Structure obj =
                new Structure
                {
                    Id = src.ID,
                    TypeId = src.TypeID,
                    Label = src.Label ?? string.Empty,
                    Notes = src.Notes ?? string.Empty,
                    Confidence = src.Confidence,
                    Verified = src.Verified,
                    Username = src.Username ?? string.Empty,
                    Attributes = src._Attributes == null
                        ? string.Empty
                        : (src.Attributes.ToXml() ?? string.Empty),
                };

            if (src.ParentID.HasValue)
                obj.ParentId = src.ParentID.Value;

            ((IChangeAction)obj).DBAction = src.DBAction;

            return obj;
        }

        IStructure IObjectConverter<StructureObj, IStructure>.Convert(StructureObj src) => Convert(src);
    }

    public class StructureServerToClientUpdater : IObjectUpdater<StructureObj, Structure>,
        IObjectUpdater<StructureObj, IStructure>
    {
        public Task<bool> Update(StructureObj obj, IStructure update)
        {
            if (update is Structure proto)
                return Update(obj, proto);
            throw new ArgumentException(
                $"Expected {nameof(Structure)}, got {update?.GetType().FullName ?? "null"}",
                nameof(update));
        }

        public async Task<bool> Update(StructureObj obj, Structure update)
        {
            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged; //Record change events so we know if an update occurred.

                obj.Confidence = update.Confidence;
                obj.Username = update.Username;
                obj.Label = update.Label;
                obj.Notes = update.Notes;
                obj.ParentID = update.HasParentId ? (long?)update.ParentId : (long?)null;
                obj.LastModified = update.LastModified.ToDateTime();
                obj.Created = update.Created.ToDateTime();
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
