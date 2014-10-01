using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.Common;
using System.Windows.Forms;
using WebAnnotationModel;
using System.Diagnostics; 

namespace WebAnnotation.ViewModel
{
    /// <summary>
    /// This class represents a link between locations. This object is a little unique because it is
    /// not tied to the database object like the other *obj classes
    /// </summary>
    public class LocationLink : Viking.Objects.UIObjBase
    {
        public override int GetHashCode()
        {
            return A.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            LocationLink link = obj as LocationLink;
            if (link == null)
                return false;

            return (link.A.ID == A.ID && link.B.ID == B.ID); 
        }

        public static bool operator ==(LocationLink A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(LocationLink A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        public override string ToString()
        {
            return A.ID.ToString() + " <-> " + B.ID.ToString() + " Sections: " + minSection.ToString() + "-" + maxSection.ToString();
        }
        
        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public Location_CanvasViewModel A; 

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public Location_CanvasViewModel B;

        public int minSection { get { return A.Section < B.Section ? A.Section : B.Section; } }
        public int maxSection { get { return A.Section > B.Section ? A.Section : B.Section; } }

        private RoundLineCode.RoundLine _lineGraphic = null;
        public RoundLineCode.RoundLine lineGraphic
        {
            get
            {
                if (_lineGraphic == null)
                {
                    _lineGraphic = new RoundLineCode.RoundLine((float)A.VolumeX,
                                                      (float)A.VolumeY,
                                                      (float)B.VolumeX,
                                                      (float)B.VolumeY);
                }

                return _lineGraphic;
            }

        }
        
        public double Radius
        {
            get
            {
                return Math.Min(A.Radius,B.Radius)/2f;
            }
        }

        public LocationLink(Location_CanvasViewModel LocOne, Location_CanvasViewModel LocTwo)
        {
            if (LocOne == null)
                throw new ArgumentNullException("LocOne");

            if (LocTwo == null)
                throw new ArgumentNullException("LocTwo"); 

            Debug.Assert(LocOne != LocTwo);
            this.A = LocOne.ID < LocTwo.ID ? LocOne : LocTwo;
            this.B = LocOne.ID > LocTwo.ID ? LocOne : LocTwo; 

            
           // lineSegment = new Geometry.GridLineSegment(A.VolumePosition, B.VolumePosition);
            
        }

        #region IUIObjectBasic Members
        
        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                MenuItem menuSeperator = new MenuItem(); 
                MenuItem menuDelete = new MenuItem("Delete Link", ContextMenu_OnDelete);

                menu.MenuItems.Add(menuSeperator); 
                menu.MenuItems.Add(menuDelete); 

                return menu; 
            }
        }

        public override string ToolTip
        {
            get { return A.ID.ToString() + " -> " + B.ID.ToString(); }
        }

        public override void Save()
        {
            throw new NotImplementedException();
        }

        #endregion

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public override void Delete()
        {
            CallBeforeDelete(); 

            Store.LocationLinks.DeleteLink(this.A.ID, this.B.ID);

            CallAfterDelete();
        }

    }
}
