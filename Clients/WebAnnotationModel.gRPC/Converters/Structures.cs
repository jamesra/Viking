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

    /// <summary>
    /// Builds a new <see cref="StructureObj"/> for a structure the client sees for the first time in a server batch
    /// (called by <see cref="StoreServerQueryResultsHandler{KEY,OBJECT,SERVER_OBJECT}"/> on a cache miss; cached
    /// objects go through <see cref="StructureServerToClientUpdater"/> instead).
    /// </summary>
    /// <remarks>
    /// The result mirrors the server: it carries the server's ParentID, so the store wires it under its parent rather
    /// than into RootObjects, and its DBAction is <see cref="DBACTION.NONE"/> so Save never sends it back.
    /// <c>DBAction</c> is assigned last in each initializer because every property setter marks the object UPDATE.
    /// </remarks>
    public class StructureServerToClientConverter : IObjectConverter<Structure, StructureObj>,
        IObjectConverter<IStructure, StructureObj>
    {
        /// <summary>Converts a proto structure, copying every field the proto carries, including the optional parent id.</summary>
        public StructureObj Convert(Structure src)
        {
            StructureObj obj =
                new StructureObj(src.Id, src.TypeId)
                {
                    Label = src.Label,
                    Notes = src.Notes,
                    Confidence = src.Confidence,
                    Username = src.Username,
                    Verified = src.Verified,
                    ParentID = src.HasParentId ? (long?)src.ParentId : (long?)null,
                    LastModified = src.LastModified?.ToDateTime() ?? default,
                    Created = src.Created?.ToDateTime() ?? default,
                    DBAction = DBACTION.NONE,
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

        /// <summary>
        /// Converts any <see cref="IStructure"/>; a proto <see cref="Structure"/> takes the full proto path,
        /// other implementations copy ID, type, label, parent and attributes.
        /// </summary>
        public StructureObj Convert(IStructure src)
        {
            if (src is Structure concrete)
                return Convert(concrete);

            StructureObj obj =
                new StructureObj((long)src.ID, src.TypeID)
                {
                    Label = src.Label,
                    ParentID = src.ParentID,
                    DBAction = DBACTION.NONE,
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

        /// <summary>
        /// Copies the server's values onto a cached structure, except fields the user edited and has not saved yet
        /// (<see cref="StructureObj.IsLocallyEdited"/>), which keep the user's value. Applied values are not local
        /// edits, so DBAction is left as it was: an unedited structure stays <see cref="DBACTION.NONE"/> and mirrors
        /// the server, as after the WCF store's InternalUpdate, and a queued UPDATE or DELETE stays queued.
        /// LastModified, Created, and attributes always take the server's value.
        /// </summary>
        /// <returns>True if any property changed.</returns>
        public async Task<bool> Update(StructureObj obj, Structure update)
        {
            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged; //Record change events so we know if an update occurred.

                obj.ApplyServerValues(() =>
                {
                    if (!obj.IsLocallyEdited(nameof(StructureObj.Confidence)))
                        obj.Confidence = update.Confidence;
                    if (!obj.IsLocallyEdited(nameof(StructureObj.Username)))
                        obj.Username = update.Username;
                    if (!obj.IsLocallyEdited(nameof(StructureObj.Label)))
                        obj.Label = update.Label;
                    if (!obj.IsLocallyEdited(nameof(StructureObj.Notes)))
                        obj.Notes = update.Notes;
                    if (!obj.IsLocallyEdited(nameof(StructureObj.ParentID)))
                        obj.ParentID = update.HasParentId ? (long?)update.ParentId : (long?)null;
                    obj.LastModified = update.LastModified.ToDateTime();
                    obj.Created = update.Created.ToDateTime();
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
