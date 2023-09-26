﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common.UI;
using WebAnnotationModel;
using SqlGeometryUtils;
using Geometry;

namespace WebAnnotation.ViewModel
{
    public class Location_ViewModelBase : Viking.Objects.UIObjBase, IEqualityComparer<Location_ViewModelBase>, IEqualityComparer<LocationObj>, IComparable<Location_ViewModelBase>, System.Windows.IWeakEventListener
    {
        public readonly LocationObj modelObj;

        public Location_ViewModelBase(long LocationID)
        {
            this.modelObj = Store.Locations.GetObjectByID(LocationID);
            if (modelObj == null)
            {
                throw new ArgumentException(string.Format("Could not load location {0} from store", LocationID));
            }
        }

        [Column("ID")]
        public long ID
        {
            get { return modelObj.ID; }
        }

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
            Location_ViewModelBase LocObj = obj as Location_ViewModelBase;
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

        public static bool operator ==(Location_ViewModelBase A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A != null)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(Location_ViewModelBase A, object B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if ((object)A != null)
                return !A.Equals(B);

            return true;
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

        private Structure _Parent = null;

        private void ResetParentCache() { _Parent = null; }

        public Structure Parent
        {
            get
            {
                if (this.modelObj.Parent == null)
                    return null;

                if (this._Parent == null)
                    _Parent = new Structure(this.modelObj.Parent);

                return _Parent;
            }
        }
        /*
        #region Weak Events
        private object EventsLock = new object();
        private bool EventsRegistered = false;
        internal void RegisterForLocationEvents()
        {
            if (EventsRegistered)
                return;

            lock (EventsLock)
            {
                if (EventsRegistered)
                    return;

                NotifyPropertyChangedEventManager.AddListener(this.modelObj, this);

                if (this.modelObj.Parent == null)
                {
                    Action<long> GetParent = delegate(long ParentID)
                    {
                        StructureObj parent = Store.Structures.GetObjectByID(ParentID, true);
                        if (parent != null)
                            NotifyPropertyChangedEventManager.AddListener(this.modelObj.Parent, this);
                    };

                    System.Threading.Tasks.Task.Run(() => GetParent(this.modelObj.ParentID.Value));
                    //AnnotationOverlay.CurrentOverlay.Parent.BeginInvoke(GetParent, new object[] { this.modelObj.ParentID.Value });
                }
                else
                    NotifyPropertyChangedEventManager.AddListener(this.modelObj.Parent, this);

                EventsRegistered = true;
            }
        }

        internal void DeregisterForLocationEvents()
        {
            if (!EventsRegistered)
                return;

            lock (EventsLock)
            {
                if (!EventsRegistered)
                    return;

                NotifyPropertyChangedEventManager.RemoveListener(this.modelObj, this);
                NotifyPropertyChangedEventManager.RemoveListener(this.modelObj.Parent, this);

                EventsRegistered = false;
            }
        }
        #endregion
        */
        #region IUIObject Members

        public override void Delete()
        {
            Store.Locations.Remove(this.modelObj);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();

            if (this.ParentID.HasValue)
                Store.Structures.CheckForOrphan(this.ParentID.Value);
        }

        public new event PropertyChangedEventHandler ValueChanged
        {
            add { modelObj.PropertyChanged += value; }
            remove { modelObj.PropertyChanged -= value; }
        }

        protected ContextMenu _AddExportMenus(ContextMenu menu)
        {
            if (Global.Export != null)
            {
                MenuItem menuExport = new MenuItem("Export");

                _AddExportToTulipURL(menuExport);

                menu.MenuItems.Add(menuExport);
            }

            return menu;
        }

        private void _AddExportToTulipURL(MenuItem menu)
        {
            MenuItem menuTulipURL = new MenuItem("Tulip URL");
            MenuItem menuMorphology = new MenuItem("Morphology", ContextMenu_ExportMorphology);

            menuTulipURL.MenuItems.Add(menuMorphology);
            _AddExportToTulipNetwork(menuTulipURL);
            menu.MenuItems.Add(menuTulipURL);
        }

        private void _AddExportToTulipNetwork(MenuItem menu)
        {
            MenuItem menuNetwork = new MenuItem("Network", ContextMenu_ExportNetwork);
            menuNetwork.Tag = new long?(); //Tag contains the number of hops

            MenuItem menuOneHop = new MenuItem("1 degree  of seperation", ContextMenu_ExportNetwork);
            menuOneHop.Tag = new long?(1);
            MenuItem menuTwoHop = new MenuItem("2 degrees of seperation", ContextMenu_ExportNetwork);
            menuTwoHop.Tag = new long?(2);
            MenuItem menuThreeHop = new MenuItem("3 degrees of seperation", ContextMenu_ExportNetwork);
            menuThreeHop.Tag = new long?(3);
            MenuItem menuAllHop = new MenuItem("All connected", ContextMenu_ExportNetwork);
            menuAllHop.Tag = new long?();

            menu.MenuItems.Add(menuNetwork);

            menuNetwork.MenuItems.Add(menuOneHop);
            menuNetwork.MenuItems.Add(menuTwoHop);
            menuNetwork.MenuItems.Add(menuThreeHop);
            menuNetwork.MenuItems.Add(menuAllHop);
        }

        protected ContextMenu _AddTerminalOffEdgeMenus(ContextMenu menu)
        {
            MenuItem menuExtensible = new MenuItem("Terminal", ContextMenu_OnTerminal);
            MenuItem menuOffEdge = new MenuItem("Off Edge", ContextMenu_OnOffEdge);

            menuExtensible.Checked = this.modelObj.Terminal;
            menuOffEdge.Checked = this.modelObj.OffEdge;

            menu.MenuItems.Add(menuExtensible);
            menu.MenuItems.Add(menuOffEdge);

            return menu;
        }

        protected ContextMenu _AddDeleteMenu(ContextMenu menu)
        {
            MenuItem menuSeperator = new MenuItem();
            MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

            menu.MenuItems.Add(menuSeperator);
            menu.MenuItems.Add(menuDelete);

            return menu;
        }

        protected ContextMenu _AddCopyLocationIDMenu(ContextMenu menu)
        {
            MenuItem menuCopyLocationID = new MenuItem(string.Format("Copy Location ID: {0}", this.ID), ContextMenu_CopyLocationID);
            menu.MenuItems.Add(menuCopyLocationID);

            return menu;
        }

        protected void _AddConvertShapeMenus(ContextMenu menu)
        {
            MenuItem menuShape = new MenuItem("Change Shape");

            if(this.TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE)
            { 
                MenuItem menuOpenCurve = new MenuItem("Curve", ContextMenu_ConvertShape);
                menuOpenCurve.Tag = Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE;
                menuShape.MenuItems.Add(menuOpenCurve);
            }

            if(this.TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE)
            { 
                MenuItem menuCircle = new MenuItem("Circle", ContextMenu_ConvertShape);
                menuCircle.Tag = Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE;
                menuShape.MenuItems.Add(menuCircle);
            }

            menu.MenuItems.Add(menuShape);
        }

        protected void _AddSimplifyPolygonMenus(ContextMenu menu)
        {
            if (this.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON ||
                this.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON)
            {
                MenuItem menuSimplify = new MenuItem("Simplify Shape", ContextMenu_SimplifyPolygon);
                menuSimplify.Tag = new int?();
                menu.MenuItems.Add(menuSimplify);
            }
        }

        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                this._AddCopyLocationIDMenu(menu);
                this._AddTerminalOffEdgeMenus(menu);
                this._AddConvertShapeMenus(menu);
                this._AddSimplifyPolygonMenus(menu);
                this._AddDeleteMenu(menu);

                return menu;
            }
        }

        public override Image SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        public override string ToolTip
        {
            get { return this.modelObj.Label; }
        }

        public override void Save()
        {
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        #endregion


        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this.Parent);
        }

        protected void ContextMenu_OnTerminal(object sender, EventArgs e)
        {
            this.modelObj.Terminal = !this.modelObj.Terminal;
            try
            {
                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                this.modelObj.Terminal = !this.modelObj.Terminal;
            }
        }

        protected void ContextMenu_CopyLocationID(object sender, EventArgs e)
        {
            System.Windows.Forms.Clipboard.SetText(this.ID.ToString());
        }

        protected void ContextMenu_ExportMorphology(object sender, EventArgs e)
        {
            Global.Export.OpenMorphology(this.ParentID.Value);
        }

        protected void ContextMenu_ExportNetwork(object sender, EventArgs e)
        {
            MenuItem item = sender as MenuItem;
            long? hops = item.Tag as long?;

            Global.Export.OpenNetwork(this.ParentID.Value, hops);
        }

        protected void ContextMenu_ConvertShape(object sender, EventArgs e)
        {
            MenuItem item = sender as MenuItem;
            Viking.AnnotationServiceTypes.Interfaces.LocationType targetShape = (Viking.AnnotationServiceTypes.Interfaces.LocationType)item.Tag;

            switch (targetShape)
            {
                case Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE:
                    break;
                case Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE:
                    break;
            }
        }

        /// <summary>
        /// Simplify the shape by removing verticies
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ContextMenu_SimplifyPolygon(object sender, EventArgs e)
        {
            //If tag is None, we simplify the exterior.  If tag is a number, we simplify that internal polygon
            MenuItem item = sender as MenuItem;
            int? innerPoly = item.Tag is null ? new int?() : (int?)item.Tag;

            var poly = this.modelObj.MosaicShape.ToPolygon();

            try { 
                if(!innerPoly.HasValue)
                {    
                    var outer_poly = new GridPolygon(poly.ExteriorRing);
                    var simple_poly = outer_poly.Simplify(Global.PenSimplifyThreshold);
                    poly.ExteriorRing = simple_poly.ExteriorRing;
                    this.modelObj.MosaicShape = poly.ToSqlGeometry();
                }
                else
                {
                    if(innerPoly.Value >= poly.InteriorRings.Count)
                    {
                        Trace.WriteLine($"Inner polygon {innerPoly.Value} does not exist");
                        return;
                    }

                    var inner_poly = poly.InteriorPolygons[innerPoly.Value];
                    var simple_inner_poly = inner_poly.Simplify(Global.PenSimplifyThreshold / 2.0);
                    poly.ReplaceInteriorRing(innerPoly.Value, simple_inner_poly);
                    this.modelObj.MosaicShape = poly.ToSqlGeometry();
                }

                Store.Locations.Save();
            }
            catch(Exception ex)
            {
                Trace.WriteLine("Could not simplify polygon");
            }
        }

        /// <summary>
        /// Simplify the shape by removing verticies
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ContextMenu_RemoveInnerPolygon(object sender, EventArgs e)
        {
            //If tag is None, we simplify the exterior.  If tag is a number, we simplify that internal polygon
            MenuItem item = sender as MenuItem;
            int? innerPoly = item.Tag is null ? new int?() : (int?)item.Tag;

            var poly = this.modelObj.MosaicShape.ToPolygon();

            try
            {
                if (!innerPoly.HasValue)
                {
                    Trace.WriteLine($"No inner polygon parameter provided");
                }
                else
                {
                    if (innerPoly.Value >= poly.InteriorRings.Count)
                    {
                        Trace.WriteLine($"Inner polygon {innerPoly.Value} does not exist");
                        return;
                    }

                    poly.RemoveInteriorRing(innerPoly.Value); 
                    this.modelObj.MosaicShape = poly.ToSqlGeometry();
                }

                Store.Locations.Save();
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Could not simplify polygon");
            }
        }

        protected void ContextMenu_OnOffEdge(object sender, EventArgs e)
        {
            this.modelObj.OffEdge = !this.modelObj.OffEdge;
            try
            {
                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                this.modelObj.OffEdge = !this.modelObj.OffEdge;
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }


        public bool Equals(Location_ViewModelBase x, Location_ViewModelBase y)
        {
            if (x == null && y == null)
                return true;

            if (x == null || y == null)
                return false;

            return x.ID == y.ID;
        }

        public int GetHashCode(Location_ViewModelBase obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj", "GetHashCode");

            return obj.modelObj.GetHashCode();
        }

        public bool Equals(LocationObj x, LocationObj y)
        {
            if (x == null && y == null)
                return true;

            if (x == null || y == null)
                return false;

            return x.ID == y.ID;
        }

        public int GetHashCode(LocationObj obj)
        {
            return obj.GetHashCode();
        }

        int IComparable<Location_ViewModelBase>.CompareTo(Location_ViewModelBase other)
        {
            if (other == null)
                return 1;

            return (int)(this.ID - other.ID);
        }

        #region WeakEvents

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            PropertyChangedEventArgs PropertyChangedArgs = e as PropertyChangedEventArgs;
            if (PropertyChangedArgs != null)
            {
                StructureObj structObj = sender as StructureObj;
                if (structObj != null && structObj.ID == this.modelObj.ParentID)
                    this.OnParentPropertyChanged(sender, PropertyChangedArgs);
                else
                {
                    this.OnObjPropertyChanged(sender, PropertyChangedArgs);
                }

                return true;
            }

            System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs = e as System.Collections.Specialized.NotifyCollectionChangedEventArgs;
            if (CollectionChangeArgs != null)
            {
                this.OnLinksChanged(sender, CollectionChangeArgs);
                return true;
            }

            Debug.Fail("Weak Event not handled");
            return false;
        }

        protected virtual void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            this.ResetParentCache();
            return;
        }

        protected virtual void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            return;
        }

        protected virtual void OnLinksChanged(object o, NotifyCollectionChangedEventArgs args)
        {
            return;
        }
        #endregion


        #region Properties



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

        [Column("Last Editor")]
        public string Username
        {
            get { return modelObj.Username; }
        }

        [Column("Modified")]
        public DateTime LastModified
        {
            get { return modelObj.LastModified; }
        }

        /// <summary>
        /// VolumeX is the x position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        /// 
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
        /// 
        [Column("VolumeY")]
        public double VolumeY
        {
            get
            {
                return modelObj.VolumePosition.Y;
            }
        }



        [Column("Width")]
        public double Width
        {
            get { return modelObj.Width.HasValue ? modelObj.Width.Value : 0; }
        }

        [Column("Radius")]
        public double Radius
        {
            get { return modelObj.Radius; }
        }


        [Column("TypeCode")]
        public Viking.AnnotationServiceTypes.Interfaces.LocationType TypeCode
        {
            get { return (Viking.AnnotationServiceTypes.Interfaces.LocationType)modelObj.TypeCode; }
        }

        public bool IsTerminal
        {
            get
            {
                return modelObj.Terminal;
            }
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

        /// <summary>
        /// Return true if the locations volume position has been calculated
        /// </summary>
        public bool VolumePositionHasBeenCalculated
        {
            get { return this.modelObj.VolumePositionHasBeenCalculated; }
        }

        #endregion

    }
}