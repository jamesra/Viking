using System;
using System.Windows.Forms;
using Viking.Common;
using Viking.Common.UI;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// One row of a structure list in the property tabs (a structure's children, a type's structures), read from OData
    /// (<see cref="StructureListODataQuery"/>). It shows the columns <see cref="Structure"/> does. Acting on the row (its
    /// context menu, deleting it, its properties) needs the whole structure, so the row loads it then, once, from the
    /// structure store (<see cref="Store.Structures"/>), which asks the server only for a structure it does not hold.
    /// </summary>
    public sealed class StructureRow : Viking.Objects.UIObjBase, IEquatable<StructureRow>
    {
        private readonly long typeID;

        /// <summary>The whole structure, once something needs it; see <see cref="Structure"/>.</summary>
        private Structure? structure;

        public StructureRow(long id, long typeID, long? parentID, string label, string username, int numLinks, bool verified,
            double confidence, ObjAttribute[] attributes, string notes)
        {
            ID = id;
            this.typeID = typeID;
            ParentID = parentID;
            InfoLabel = label;
            Username = username;
            NumLinks = numLinks;
            Verified = verified;
            Confidence = confidence;
            AttributeList = attributes;
            Notes = notes;
        }

        public long ID { get; }

        [Column("Label")]
        public string InfoLabel { get; }

        [Column("ParentID")]
        public long? ParentID { get; }

        [Column("Last Editor")]
        public string Username { get; }

        [Column("Num Links")]
        public int NumLinks { get; }

        [Column("Verified")]
        public bool Verified { get; }

        [Column("Confidence")]
        public double Confidence { get; }

        public ObjAttribute[] AttributeList { get; }

        /// <summary>The attributes as the structure's tags read, separated by commas.</summary>
        [Column("Attributes")]
        public string Attributes => string.Join(", ", (object[])AttributeList);

        [Column("Notes")]
        public string Notes { get; }

        /// <summary>The type's name from the types Viking has loaded (no server call per row), or its ID.</summary>
        [Column("Type")]
        public string Type => Store.StructureTypes.GetObjectByID(typeID, false)?.Name ?? typeID.ToString();

        public override string ToString() => ID.ToString();

        /// <summary>
        /// The whole structure as the other structure views show it, loaded from the store on first use; null when the server
        /// no longer has it or cannot be reached. Runs on the calling thread.
        /// </summary>
        public Structure? Structure
        {
            get
            {
                if (structure is null && Store.Structures.GetObjectByID(ID) is StructureObj obj)
                {
                    structure = new Structure(obj);
                }

                return structure;
            }
        }

        public override ContextMenuStrip ContextMenu => Structure?.ContextMenu ?? new ContextMenuStrip();

        public override string ToolTip => ToString();

        public override void Delete() => Structure?.Delete();

        public override void Save() => Structure?.Save();

        public override void ShowProperties() => Structure?.ShowProperties();

        public bool Equals(StructureRow? other) => other is not null && other.ID == ID;

        public override bool Equals(object? obj) => Equals(obj as StructureRow);

        public override int GetHashCode() => ID.GetHashCode();
    }
}
