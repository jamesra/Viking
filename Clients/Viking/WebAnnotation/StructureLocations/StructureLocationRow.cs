using System;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.Common;
using Viking.Common.UI;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.StructureLocations
{
    /// <summary>
    /// One row of a structure's Locations tab, read from OData (<see cref="StructureLocationsODataQuery"/>) without the
    /// location's shapes. It shows the same columns <see cref="Location_ViewModelBase"/> does. Acting on the row (its
    /// context menu, deleting it, its properties) needs the whole location, so the row loads it then, once, from the
    /// location store (<see cref="Store.Locations"/>), which asks the server only for a location it does not hold.
    /// </summary>
    public sealed class StructureLocationRow : Viking.Objects.UIObjBase, IEquatable<StructureLocationRow>
    {
        private readonly bool terminal;
        private readonly bool offEdge;
        private readonly bool varicosityCap;
        private readonly bool untraceable;
        private readonly int linkCount;

        /// <summary>The whole location, once something needs it; see <see cref="Location"/>.</summary>
        private Location_PropertyPageViewModel? location;

        public StructureLocationRow(long id, long parentID, double x, double y, double z, double volumeX, double volumeY,
            double width, double radius, LocationType typeCode, bool terminal, bool offEdge, bool varicosityCap,
            bool untraceable, int linkCount, string username, DateTime lastModified)
        {
            ID = id;
            ParentID = parentID;
            X = x;
            Y = y;
            Z = z;
            VolumeX = volumeX;
            VolumeY = volumeY;
            Width = width;
            Radius = radius;
            TypeCode = typeCode;
            this.terminal = terminal;
            this.offEdge = offEdge;
            this.varicosityCap = varicosityCap;
            this.untraceable = untraceable;
            this.linkCount = linkCount;
            Username = username;
            LastModified = lastModified;
        }

        [Column("ID")]
        public long ID { get; }

        public long ParentID { get; }

        [Column("X")]
        public double X { get; }

        [Column("Y")]
        public double Y { get; }

        [Column("Z")]
        public double Z { get; }

        [Column("Last Editor")]
        public string Username { get; }

        [Column("Modified")]
        public DateTime LastModified { get; }

        /// <summary>The stored volume position cache, shown as a value only (never used to draw or navigate).</summary>
        [Column("VolumeX")]
        public double VolumeX { get; }

        /// <summary>The stored volume position cache, shown as a value only (never used to draw or navigate).</summary>
        [Column("VolumeY")]
        public double VolumeY { get; }

        [Column("Width")]
        public double Width { get; }

        [Column("Radius")]
        public double Radius { get; }

        [Column("TypeCode")]
        public LocationType TypeCode { get; }

        /// <summary>
        /// One link or none and no mark that the end is real (terminal, off edge, varicosity cap, untraceable): tracing may
        /// have stopped here by mistake. The same rule as <see cref="LocationObj.IsUnverifiedTerminal"/>.
        /// </summary>
        [Column("IsUnverifiedTerminal")]
        public bool IsUnverifiedTerminal => linkCount < 2 && !(terminal || offEdge || varicosityCap || untraceable);

        [Column("Section")]
        public int Section => (int)Z;

        public override string ToString() => ID.ToString();

        /// <summary>
        /// The whole location as the other location views show it, loaded from the store on first use; null when the server
        /// no longer has it (deleted since the list loaded) or cannot be reached. Runs on the calling thread.
        /// </summary>
        public Location_PropertyPageViewModel? Location
        {
            get
            {
                if (location is null && Store.Locations.GetObjectByID(ID) is not null)
                {
                    location = new Location_PropertyPageViewModel(ID);
                }

                return location;
            }
        }

        public override ContextMenuStrip ContextMenu => Location?.ContextMenu ?? new ContextMenuStrip();

        public override string ToolTip => ToString();

        public override void Delete() => Location?.Delete();

        public override void Save() => Location?.Save();

        public override void ShowProperties() => Location?.ShowProperties();

        public bool Equals(StructureLocationRow? other) => other is not null && other.ID == ID;

        public override bool Equals(object? obj) => Equals(obj as StructureLocationRow);

        public override int GetHashCode() => ID.GetHashCode();
    }
}
