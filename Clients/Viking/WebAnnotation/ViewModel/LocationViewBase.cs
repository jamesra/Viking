using System;
using System.ComponentModel; 
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; 
using Viking.Common;
using WebAnnotation;
using WebAnnotationModel;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using Common.UI;
using WebAnnotation.UI.Commands; 


namespace WebAnnotation.ViewModel
{
    /*
    class LocationViewBase : Viking.Objects.UIObjBase, IEqualityComparer<Location>, IEqualityComparer<LocationObj>, IComparable<Location>
    {
        public readonly LocationObj modelObj;

        public override string ToString()
        {
            return modelObj.ToString(); 
        }

        public override int GetHashCode()
        {
            return modelObj.GetHashCode(); 
        }

        public override bool Equals(object obj)
        {
            Location LocObj = obj as Location;
            if (LocObj != null)
            {
                return modelObj.Equals(LocObj.modelObj); 
            }

            LocationObj LocObj2 = obj as LocationObj;
            if (LocObj2 != null)
            {
                return modelObj.Equals(LocObj2); 
            }

            return false; 
        }

        public static bool operator ==(Location A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(Location A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
        }

        public virtual VertexPositionColorTexture[] Verts{ get;}
        public virtual VertexPositionColorTexture[] Indicies{ get;}

         [Column("ID")]
        public long ID
        {
            get { return modelObj.ID; }
        }

        public string Label
        {
            get
            {
                if (Parent == null)
                    return "";

                if (Parent.Type == null)
                    return "";

                return Parent.Type.Code + " " + Parent.ID.ToString();
            }
        }

        public long? ParentID
        {
            get { return modelObj.ParentID; }
        }

        public Structure Parent
        {
            get
            {
                if (this.modelObj.Parent != null)
                    return new Structure(this.modelObj.Parent);
                else
                    return null; 
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<long> Links
        {
            get { return modelObj.Links; }
        }
        
        [Column("X")]
        public double X
        {
            get { return modelObj.Position.X; }
        }

        [Column("Y")]
        public double Y
        {
            get { return modelObj.Position.Y; }
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        [Column("Z")]
        public double Z
        {
            get { return modelObj.Z; }
        }

        public GridVector2 SectionPosition
        {
            get
            {
                return modelObj.Position;
            }
            set
            {
                modelObj.Position = value;
            }
        }

        public GridVector2 VolumePosition
        {
            get
            {
                return modelObj.VolumePosition;
            }
            set
            {
                modelObj.VolumePosition = value; 
            }
        }
    */
    
        /// <summary>
        /// VolumeX is the x position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        /*
        [Column("VolumeX")]
        public double VolumeX
        {
            get
            {
                return modelObj.VolumePosition.X;
            }
        }

        /// <summary>
        /// VolumeY is the y position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        
    /*
        [Column("VolumeY")]
        public double VolumeY
        {
            get
            {
                return modelObj.VolumePosition.Y;
            }
        }

        [Column("Radius")]
        public double Radius
        {
            get { return modelObj.Radius; }
            set
            {
                if (modelObj.Radius == value)
                    return;

                modelObj.Radius = value;
            }
        }

        [Column("TypeCode")]
        public LocationType TypeCode
        {
            get { return (LocationType)modelObj.TypeCode; }
        }

        /// <summary>
        /// This column is set to true when the location has one link and is not marked as terminal.  It means the
        /// Location is a dead-end and the user did not mark it as a dead end, which means it may actually continue
        /// and the user was distracted
        /// </summary>
        /// 
        [Column("IsUnverifiedTerminal")]
        public bool IsUnverifiedTerminal
        {
            get
            {
                return modelObj.IsUnverifiedTerminal; 
            }
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        /// 

        [Column("Section")]
        public int Section
        {
            get { return (int)modelObj.Section; }
        }

        public LocationViewBase(LocationObj location)
        {
            Debug.Assert(location != null);

            this.modelObj = location; 
        }

        public abstract bool Contains(WebAnnotation.ViewModel.SectionLocationsViewModel sectionAnnotations, GridVector2 pos);

        public abstract double Radius {get;set;}        
     * }
     */
}
