using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common;
using Viking.Common.UI;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
using System.Linq;
using Microsoft.Xna.Framework;
using WebAnnotation.View;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands.Segmentation;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.ViewModel
{
    public class Location_ViewModelBase : Viking.Objects.UIObjBase, IEqualityComparer<Location_ViewModelBase>, IEqualityComparer<LocationObj>, IComparable<Location_ViewModelBase>, System.Windows.IWeakEventListener, IContextMenu
    {
        public readonly LocationObj modelObj;

        public Location_ViewModelBase(long LocationID)
        {
            if (!Store.Locations.TryGetObjectByID(LocationID, out modelObj) || modelObj is null)
            {
                throw new ArgumentException($"Could not load location {LocationID} from store");
            }
        }

        [Column("ID")]
        public long ID => modelObj.ID;

        public override string ToString() => modelObj.ToString();

        public override int GetHashCode() => modelObj.GetHashCode();

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

        public static bool operator ==(Location_ViewModelBase? A, object? B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if (A is not null)
            {
                return A.Equals(B);
            }

            return false;
        }

        public static bool operator !=(Location_ViewModelBase? A, object? B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if (A is not null)
            {
                return !A.Equals(B);
            }

            return true;
        }

        public string Label
        {
            get
            {
                if (Parent is null)
                {
                    return "";
                }

                if (Parent.Type is null)
                {
                    return "";
                }

                return Parent.Type.Code + " " + Parent.ID.ToString();
            }
        }

        public long? ParentID => modelObj.ParentID;

        private Structure? _Parent = null;

        private void ResetParentCache() => _Parent = null;

        public Structure Parent
        {
            get
            {
                if (modelObj.Parent is null)
                {
                    return null;
                }

                _Parent ??= new Structure(modelObj.Parent);

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

                if (this.modelObj.Parent is null)
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        StructureObj parent = await Store.Structures.GetObjectByID(this.modelObj.ParentID.Value);
                        if (parent != null)
                            NotifyPropertyChangedEventManager.AddListener(this.modelObj.Parent, this);
                    });
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
            Store.Locations.Remove(modelObj);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();

            if (ParentID.HasValue)
            {
                Store.Structures.CheckForOrphan(ParentID.Value);
            }
        }

        public new event PropertyChangedEventHandler ValueChanged
        {
            add => modelObj.PropertyChanged += value;
            remove => modelObj.PropertyChanged -= value;
        }

        protected ContextMenuStrip _AddExportMenus(ContextMenuStrip menu)
        {
            if (Global.Export != null)
            {
                ToolStripMenuItem menuExport = new("Export");

                _AddExportToTulipURL(menuExport);

                menu.Items.Add(menuExport);
            }

            return menu;
        }

        private void _AddExportToTulipURL(ToolStripMenuItem menu)
        {
            ToolStripMenuItem menuTulipURL = new("Tulip URL");
            ToolStripMenuItem menuMorphology = new("Morphology");
            menuMorphology.Click += ContextMenu_ExportMorphology;

            menuTulipURL.DropDownItems.Add(menuMorphology);
            _AddExportToTulipNetwork(menuTulipURL);
            menu.DropDownItems.Add(menuTulipURL);
        }

        private void _AddExportToTulipNetwork(ToolStripMenuItem menu)
        {
            ToolStripMenuItem menuNetwork = new("Network")
            {
                Tag = new long?() //Tag contains the number of hops
            };
            menuNetwork.Click += ContextMenu_ExportNetwork;

            ToolStripMenuItem menuOneHop = new("1 degree  of seperation")
            {
                Tag = new long?(1)
            };
            menuOneHop.Click += ContextMenu_ExportNetwork;
            ToolStripMenuItem menuTwoHop = new("2 degrees of seperation")
            {
                Tag = new long?(2)
            };
            menuTwoHop.Click += ContextMenu_ExportNetwork;
            ToolStripMenuItem menuThreeHop = new("3 degrees of seperation")
            {
                Tag = new long?(3)
            };
            menuThreeHop.Click += ContextMenu_ExportNetwork;
            ToolStripMenuItem menuAllHop = new("All connected")
            {
                Tag = new long?()
            };
            menuAllHop.Click += ContextMenu_ExportNetwork;

            menu.DropDownItems.Add(menuNetwork);

            menuNetwork.DropDownItems.Add(menuOneHop);
            menuNetwork.DropDownItems.Add(menuTwoHop);
            menuNetwork.DropDownItems.Add(menuThreeHop);
            menuNetwork.DropDownItems.Add(menuAllHop);
        }

        protected ContextMenuStrip _AddTerminalOffEdgeMenus(ContextMenuStrip menu)
        {
            ToolStripMenuItem menuExtensible = new("Terminal");
            menuExtensible.Click += ContextMenu_OnTerminal;
            ToolStripMenuItem menuOffEdge = new("Off Edge");
            menuOffEdge.Click += ContextMenu_OnOffEdge;

            menuExtensible.Checked = modelObj.Terminal;
            menuOffEdge.Checked = modelObj.OffEdge;

            menu.Items.Add(menuExtensible);
            menu.Items.Add(menuOffEdge);

            return menu;
        }

        protected ContextMenuStrip _AddDeleteMenu(ContextMenuStrip menu)
        {
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem menuDelete = new("Delete");
            menuDelete.Click += ContextMenu_OnDelete;

            menu.Items.Add(menuDelete);

            return menu;
        }

        protected ContextMenuStrip _AddCopyLocationIDMenu(ContextMenuStrip menu)
        {
            ToolStripMenuItem menuCopyLocationID = new($"Copy Location ID: {ID}");
            menuCopyLocationID.Click += ContextMenu_CopyLocationID;
            menu.Items.Add(menuCopyLocationID);

            return menu;
        }

        protected void _AddConvertShapeMenus(ContextMenuStrip menu)
        {
            ToolStripMenuItem menuShape = new("Change Shape");

            if (TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE)
            {
                ToolStripMenuItem menuOpenCurve = new("Curve")
                {
                    Tag = Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE
                };
                menuOpenCurve.Click += ContextMenu_ConvertShape;
                menuShape.DropDownItems.Add(menuOpenCurve);
            }

            if (TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE)
            {
                ToolStripMenuItem menuCircle = new("Circle")
                {
                    Tag = Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE
                };
                menuCircle.Click += ContextMenu_ConvertShape;
                menuShape.DropDownItems.Add(menuCircle);
            }

            // Add segmentation option for circles
            if (TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE &&
                Global.IsSegmentationServiceAvailable)
            {
                ToolStripMenuItem menuSegmentCircle = new("Segment to Polygon...");
                menuSegmentCircle.Click += ContextMenu_SegmentCircleToPolygon;
                menuShape.DropDownItems.Add(menuSegmentCircle);
            }

            // Add segmentation option for circles
            if ((TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON ||
                TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON) &&
                Global.IsSegmentationServiceAvailable)
            {
                ToolStripMenuItem menuSegmentPoly = new("Resegment...");
                menuSegmentPoly.Click += ContextMenu_SegmentPolygon;
                menuShape.DropDownItems.Add(menuSegmentPoly);
            }

            menu.Items.Add(menuShape);
        }

        protected void _AddSimplifyPolygonMenus(ContextMenuStrip menu)
        {
            if (TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON ||
                TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON)
            {
                ToolStripMenuItem menuSimplify = new("Simplify Shape")
                {
                    Tag = new int?()
                };
                menuSimplify.Click += ContextMenu_SimplifyPolygon;
                menu.Items.Add(menuSimplify);
            }
        }

        protected void _AddRandomColorMenu(ContextMenuStrip menu)
        {
            if (TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON ||
                TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON)
            {
                ToolStripMenuItem menuRandomColor = new("Random Color");
                menuRandomColor.Click += ContextMenu_RandomColor;
                menu.Items.Add(menuRandomColor);
            }
        }

        public override ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();
                ToolStripMenuItem propertiesItem = new("Properties");
                propertiesItem.Click += ContextMenu_OnProperties;
                menu.Items.Add(propertiesItem);

                _AddCopyLocationIDMenu(menu);
                _AddTerminalOffEdgeMenus(menu);
                _AddConvertShapeMenus(menu);
                _AddSimplifyPolygonMenus(menu);
                _AddRandomColorMenu(menu);
                _AddDeleteMenu(menu);

                return menu;
            }
        }

        public override Image SmallThumbnail => throw new NotImplementedException();

        public override string ToolTip => modelObj.Label;

        public override void Save() => AnnotationOverlay.SaveLocationsWithMessageBoxOnError();

        #endregion


        protected void ContextMenu_OnProperties(object sender, EventArgs e) => Viking.UI.Forms.PropertySheetForm.Show(Parent);

        protected void ContextMenu_OnTerminal(object sender, EventArgs e)
        {
            modelObj.Terminal = !modelObj.Terminal;
            try
            {
                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                modelObj.Terminal = !modelObj.Terminal;
            }
        }

        protected void ContextMenu_CopyLocationID(object sender, EventArgs e) => System.Windows.Forms.Clipboard.SetText(ID.ToString());

        protected void ContextMenu_ExportMorphology(object sender, EventArgs e)
        {
            if (!ParentID.HasValue) return;
            Global.Export.OpenMorphology(ParentID.Value);
        }

        protected void ContextMenu_ExportNetwork(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            long? hops = item.Tag as long?;

            Global.Export.OpenNetwork(ParentID.Value, hops);
        }

        protected void ContextMenu_ConvertShape(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            Viking.AnnotationServiceTypes.Interfaces.LocationType targetShape = (Viking.AnnotationServiceTypes.Interfaces.LocationType)item.Tag;

            switch (targetShape)
            {
                case Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE:
                    this.modelObj.TypeCode = Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE;
                    LocationActions.UpdateCircleLocationNoSaveCallback(this.modelObj, new Geometry.Vector2(VolumeX, VolumeY), new Geometry.Vector2(X, Y));
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
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            int? innerPoly = item.Tag is null ? new int?() : (int?)item.Tag;

            Polygon poly = modelObj.MosaicShape.ToPolygon();

            try
            {
                if (!innerPoly.HasValue)
                {
                    Polygon outer_poly = new(poly.ExteriorRing);
                    Polygon simple_poly = outer_poly.Simplify(Global.PenSimplifyThreshold);
                    poly.ExteriorRing = simple_poly.ExteriorRing;
                    modelObj.MosaicShape = poly.ToSqlGeometry().ToShape2D();
                }
                else
                {
                    if (innerPoly.Value >= poly.InteriorRings.Count)
                    {
                        Trace.WriteLine($"Inner polygon {innerPoly.Value} does not exist");
                        return;
                    }

                    Polygon inner_poly = poly.InteriorPolygons[innerPoly.Value];
                    Polygon simple_inner_poly = inner_poly.Simplify(Global.PenSimplifyThreshold / 2.0);
                    poly.ReplaceInteriorRing(innerPoly.Value, simple_inner_poly);
                    modelObj.MosaicShape = poly.ToSqlGeometry().ToShape2D();
                }

                Store.Locations.Save();
            }
            catch (Exception)
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
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            int? innerPoly = item.Tag is null ? new int?() : (int?)item.Tag;

            Polygon poly = modelObj.MosaicShape.ToPolygon();

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
                    modelObj.MosaicShape = poly.ToSqlGeometry().ToShape2D();
                }

                Store.Locations.Save();
            }
            catch (Exception)
            {
                Trace.WriteLine("Could not simplify polygon");
            }
        }

        /// <summary>
        /// Launch segmentation command to convert a circle location to a polygon using AI segmentation
        /// </summary>
        protected void ContextMenu_SegmentCircleToPolygon(object sender, EventArgs e)
        {
            try
            {
                var parent = AnnotationOverlay.CurrentOverlay.Parent;
                // Get the circle geometry
                Circle mosaic_circle = GetCircleFromLocation();

                // Generate foreground points: center + 8 points at radius/2
                List<Geometry.Vector2> foregroundPoints = [mosaic_circle.Center];

                double innerRadius = mosaic_circle.Radius / 2.0;
                for (int i = 0; i < 8; i++)
                {
                    double angle = (2.0 * Math.PI * i) / 8.0;
                    double x = mosaic_circle.Center.X + innerRadius * Math.Cos(angle);
                    double y = mosaic_circle.Center.Y + innerRadius * Math.Sin(angle);
                    foregroundPoints.Add(new Geometry.Vector2(x, y));
                }

                innerRadius = 3 * mosaic_circle.Radius / 4.0;
                for (int i = 0; i < 8; i++)
                {
                    double angle = (2.0 * Math.PI * i) / 8.0;
                    double x = mosaic_circle.Center.X + innerRadius * Math.Cos(angle);
                    double y = mosaic_circle.Center.Y + innerRadius * Math.Sin(angle);
                    foregroundPoints.Add(new Geometry.Vector2(x, y));
                }

                var success = parent.Section.ActiveSectionToVolumeTransform.TrySectionToVolume([.. foregroundPoints], out var volume_points);
                //Remove points that did not map
                volume_points = [.. volume_points.Where((p, i) => success[i])];

                // Create callback to update location shape
                void callback(Polygon outputPolygon)
                {
                    UpdateLocationShapeFromVolumePolygon(outputPolygon);
                }

                // Launch segmentation command
                var channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                long[]? includeStructureIds = modelObj.ParentID.HasValue ? [modelObj.ParentID.Value] : null;
                SegmentationCommand segmentCommand = new(
                    parent,
                    volume_points,
                    Array.Empty<Geometry.Vector2>(), // no background points initially
                    callback,
                    channelManager,
                    [modelObj.Parent.TypeID],
                    includeStructureIds);

                parent.CurrentCommand = segmentCommand;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching segmentation: {ex.Message}");
                MessageBox.Show($"Failed to launch segmentation: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Launch segmentation command to convert a circle location to a polygon using AI segmentation
        /// </summary>
        protected void ContextMenu_SegmentPolygon(object sender, EventArgs e)
        {
            try
            {
                //ContextMenu menu = sender as ContextMenu;
                if (this.modelObj.TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON &&
                   this.modelObj.TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON)
                    return;

                // Get the circle geometry
                Polygon poly = modelObj.VolumeShape.ToPolygon();
                var medial_axis = Geometry.MedialAxisFinder.ApproximateMedialAxis(poly);
                var medial_axis_points = medial_axis.Points;

                // Create callback to update location shape
                void callback(Polygon volume_poly)
                {
                    UpdateLocationShapeFromVolumePolygon(volume_poly);
                }

                // Launch segmentation command
                var parent = AnnotationOverlay.CurrentOverlay.Parent;
                var channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                long[]? includeStructureIds = modelObj.ParentID.HasValue ? [modelObj.ParentID.Value] : null;
                SegmentationCommand segmentCommand = new(
                    parent,
                    medial_axis_points,
                    Array.Empty<Geometry.Vector2>(), // no background points initially
                    callback,
                    channelManager,
                    [modelObj.Parent.TypeID],
                    includeStructureIds);

                parent.CurrentCommand = segmentCommand;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching segmentation: {ex.Message}");
                MessageBox.Show($"Failed to launch segmentation: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Extract Circle geometry from a circle location
        /// </summary>
        private Circle GetCircleFromLocation()
        {
            if (modelObj.TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE)
            {
                throw new InvalidOperationException("Location is not a circle");
            }

            Geometry.Vector2 center = modelObj.Position;
            double radius = modelObj.Radius;

            return new Circle(center, radius);
        }

        /// <summary>
        /// Update the location's shape from the segmented polygon and save
        /// </summary>
        private void UpdateLocationShapeFromMosaicPolygon(Polygon mosaic_poly)
        {
            try
            {
                // Convert location type to POLYGON
                var parent = AnnotationOverlay.CurrentOverlay.Parent;
                modelObj.TypeCode = Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON;

                modelObj.SetShapeFromGeometryInSection(parent.Section.ActiveSectionToVolumeTransform, mosaic_poly.ToSqlGeometry());
                // Save the location
                Store.Locations.Save();

                Debug.WriteLine($"Successfully converted circle location {modelObj.ID} to polygon");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating location shape: {ex.Message}");
                MessageBox.Show($"Failed to update location shape: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Update the location's shape from the segmented polygon and save
        /// </summary>
        private void UpdateLocationShapeFromVolumePolygon(Polygon volume_poly)
        {
            try
            {
                // Convert location type to POLYGON
                modelObj.TypeCode = Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON;

                var parent = AnnotationOverlay.CurrentOverlay.Parent;
                var mosaic_poly = parent.Section.ActiveSectionToVolumeTransform.TryMapShapeVolumeToSection(volume_poly);

                modelObj.SetShapeFromGeometryInVolume(parent.Section.ActiveSectionToVolumeTransform, volume_poly.ToSqlGeometry());

                // Save the location
                Store.Locations.Save();

                Debug.WriteLine($"Successfully converted circle location {modelObj.ID} to polygon");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating location shape: {ex.Message}");
                MessageBox.Show($"Failed to update location shape: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected void ContextMenu_OnOffEdge(object sender, EventArgs e)
        {
            modelObj.OffEdge = !modelObj.OffEdge;
            try
            {
                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                modelObj.OffEdge = !modelObj.OffEdge;
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e) => Delete();

        protected void ContextMenu_RandomColor(object sender, EventArgs e)
        {
            try
            {
                var overlay = AnnotationOverlay.CurrentOverlay;
                if (overlay is null)
                    return;

                var sectionView = AnnotationOverlay.GetAnnotationsForSection((int)modelObj.Z);
                if (sectionView is null)
                    return;

                if (sectionView.TryGetLocation(modelObj.ID, out LocationCanvasView locView))
                {
                    if (locView is LocationPolygonView polygonView)
                    {
                        // Generate random color while preserving alpha
                        float currentAlpha = polygonView.Color.GetAlpha();
                        Microsoft.Xna.Framework.Color newColor = Microsoft.Xna.Framework.Color.Black.Random().SetAlpha(currentAlpha);
                        polygonView.Color = newColor;

                        // Invalidate to trigger redraw
                        overlay.Parent?.Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting random color: {ex.Message}");
            }
        }


        public bool Equals(Location_ViewModelBase x, Location_ViewModelBase y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.ID == y.ID;
        }

        public int GetHashCode(Location_ViewModelBase obj)
        {
            if (obj is null)
            {
                throw new ArgumentNullException("obj", "GetHashCode");
            }

            return obj.modelObj.GetHashCode();
        }

        public bool Equals(LocationObj x, LocationObj y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.ID == y.ID;
        }

        public int GetHashCode(LocationObj obj) => obj.GetHashCode();

        int IComparable<Location_ViewModelBase>.CompareTo(Location_ViewModelBase other)
        {
            if (other is null)
            {
                return 1;
            }

            return (int)(ID - other.ID);
        }

        #region WeakEvents

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (e is PropertyChangedEventArgs PropertyChangedArgs)
            {
                StructureObj structObj = sender as StructureObj;
                if (structObj != null && structObj.ID == modelObj.ParentID)
                {
                    OnParentPropertyChanged(sender, PropertyChangedArgs);
                }
                else
                {
                    OnObjPropertyChanged(sender, PropertyChangedArgs);
                }

                return true;
            }

            if (e is System.Collections.Specialized.NotifyCollectionChangedEventArgs CollectionChangeArgs)
            {
                OnLinksChanged(sender, CollectionChangeArgs);
                return true;
            }

            Debug.Fail("Weak Event not handled");
            return false;
        }

        protected virtual void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            ResetParentCache();
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
        public double X => modelObj.Position.X;

        [Column("Y")]
        public double Y => modelObj.Position.Y;

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        [Column("Z")]
        public double Z => modelObj.Z;

        [Column("Last Editor")]
        public string Username => modelObj.Username;

        [Column("Modified")]
        public DateTime LastModified => modelObj.LastModified;

        /// <summary>
        /// VolumeX is the x position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        /// 
        [Column("VolumeX")]
        public double VolumeX => modelObj.VolumePosition.X;

        /// <summary>
        /// VolumeY is the y position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        /// 
        [Column("VolumeY")]
        public double VolumeY => modelObj.VolumePosition.Y;



        [Column("Width")]
        public double Width => modelObj.Width ?? 0;

        [Column("Radius")]
        public double Radius => modelObj.Radius;


        [Column("TypeCode")]
        public Viking.AnnotationServiceTypes.Interfaces.LocationType TypeCode => modelObj.TypeCode;

        public bool IsTerminal => modelObj.Terminal;

        /// <summary>
        /// This column is set to true when the location has one link and is not marked as terminal.  It means the
        /// Location is a dead-end and the user did not mark it as a dead end, which means it may actually continue
        /// and the user was distracted
        /// </summary>
        /// 
        [Column("IsUnverifiedTerminal")]
        public bool IsUnverifiedTerminal => modelObj.IsUnverifiedTerminal;

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        /// 

        [Column("Section")]
        public int Section => (int)modelObj.Section;

        /// <summary>
        /// Return true if the locations volume position has been calculated
        /// </summary>
        public bool VolumePositionHasBeenCalculated => modelObj.VolumePositionHasBeenCalculated;

        #endregion

    }
}
