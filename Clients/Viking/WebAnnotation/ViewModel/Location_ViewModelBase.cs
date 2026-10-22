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
    public class Location_ViewModelBase : Viking.Objects.UIObjBase, IEqualityComparer<Location_ViewModelBase>, IEqualityComparer<LocationObj>, System.Windows.IWeakEventListener, IContextMenu
    {
        public readonly LocationObj modelObj;

        public Location_ViewModelBase(long LocationID)
        {
            modelObj = Store.Locations.GetObjectByID(LocationID);
            if (modelObj is null)
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

            ToolStripMenuItem menuOneHop = new("1 degree of separation")
            {
                Tag = new long?(1)
            };
            menuOneHop.Click += ContextMenu_ExportNetwork;
            ToolStripMenuItem menuTwoHop = new("2 degrees of separation")
            {
                Tag = new long?(2)
            };
            menuTwoHop.Click += ContextMenu_ExportNetwork;
            ToolStripMenuItem menuThreeHop = new("3 degrees of separation")
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

        protected ContextMenuStrip _AddOpenInSbfsemToolsMenu(ContextMenuStrip menu)
        {
            long? cellId = SbfsemToolsLauncher.GetRootStructureIdForLocation(modelObj);
            if (cellId.HasValue)
                SbfsemToolsLauncher.AddOpenMenuItem(menu, cellId.Value, ID);

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

            // Manual conversion needs no segmentation service or review role, so every annotator with write access gets it.
            if (TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE)
            {
                ToolStripMenuItem menuConvertCircle = new("Convert to Polygon");
                menuConvertCircle.Click += ContextMenu_ConvertCircleToPolygon;
                menuShape.DropDownItems.Add(menuConvertCircle);
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

            if (TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON ||
                TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON)
            {
                ToolStripMenuItem menuEditMask = new("Dilate/erode mask...");
                menuEditMask.Click += ContextMenu_EditPolygonMask;
                menuShape.DropDownItems.Add(menuEditMask);
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
                _AddOpenInSbfsemToolsMenu(menu);
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
            bool oldTerminal = modelObj.Terminal;
            bool oldOffEdge = modelObj.OffEdge;
            modelObj.Terminal = !modelObj.Terminal;
            try
            {
                Store.Locations.Save();
                UndoManager.PushLocationFlags("Toggle Terminal", modelObj, oldTerminal, oldOffEdge);
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
                    modelObj.MosaicShape = poly.ToSqlGeometry();
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
                    modelObj.MosaicShape = poly.ToSqlGeometry();
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
                    modelObj.MosaicShape = poly.ToSqlGeometry();
                }

                Store.Locations.Save();
            }
            catch (Exception)
            {
                Trace.WriteLine("Could not simplify polygon");
            }
        }

        /// <summary>
        /// Number of vertices in the polygon that replaces a circle in <see cref="ContextMenu_ConvertCircleToPolygon"/>.
        /// </summary>
        private const int ManualCirclePolygonVertexCount = 24;

        /// <summary>
        /// Replaces this circle with a polygon that traces its outline, with no segmentation service involved.
        /// The ring is built in mosaic space, mapped to volume space, and saved through
        /// <see cref="LocationShapeUpdate.ApplyVolumePolygon"/> like Segment to Polygon, so the user can then
        /// reshape it with the normal polygon vertex tools. Runs on the UI thread from the Change Shape menu.
        /// </summary>
        protected void ContextMenu_ConvertCircleToPolygon(object sender, EventArgs e)
        {
            try
            {
                var parent = AnnotationOverlay.CurrentOverlay?.Parent;
                if (parent?.Section is null)
                {
                    MessageBox.Show("There is no open section viewer to convert in.", "Convert to Polygon", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Circle mosaicCircle = GetCircleFromLocation();
                Geometry.Vector2[] mosaicRing = new Geometry.Vector2[ManualCirclePolygonVertexCount + 1];
                for (int i = 0; i < ManualCirclePolygonVertexCount; i++)
                {
                    double angle = 2.0 * Math.PI * i / ManualCirclePolygonVertexCount;
                    mosaicRing[i] = new Geometry.Vector2(
                        mosaicCircle.Center.X + mosaicCircle.Radius * Math.Cos(angle),
                        mosaicCircle.Center.Y + mosaicCircle.Radius * Math.Sin(angle));
                }

                mosaicRing[ManualCirclePolygonVertexCount] = mosaicRing[0];

                IReadOnlyList<Geometry.Vector2> volumeRing = CircleSegmentationPrompts.ToVolumePoints(
                    mosaicRing,
                    parent.Section.ActiveSectionToVolumeTransform);
                if (volumeRing.Count != mosaicRing.Length)
                {
                    MessageBox.Show("This circle could not be mapped onto the volume.", "Convert to Polygon", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                UpdateLocationShapeFromVolumePolygon(new Polygon(volumeRing.ToArray()));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting circle to polygon: {ex.Message}");
                MessageBox.Show($"Failed to convert to polygon: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Launch segmentation command to convert a circle location to a polygon using AI segmentation
        /// </summary>
        protected void ContextMenu_SegmentCircleToPolygon(object sender, EventArgs e)
        {
            try
            {
                var parent = AnnotationOverlay.CurrentOverlay?.Parent;
                if (parent is null)
                {
                    MessageBox.Show("There is no open section viewer to segment in.", "Segment", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Get the circle geometry
                // The same starting prompt auto-polygonize sends: the inscribed square and four axis clicks.
                Circle mosaic_circle = GetCircleFromLocation();
                if (!CircleSegmentationPrompts.TryCreateStartingPrompt(
                        CircleSegmentationPrompts.ToVolumePoints(
                            CircleSegmentationPrompts.CreateMosaicRadiusPoints(mosaic_circle),
                            parent.Section.ActiveSectionToVolumeTransform),
                        out CircleSegmentationPrompts.StartingPrompt startingPrompt))
                {
                    MessageBox.Show("This circle could not be mapped onto the volume.", "Segment", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                IReadOnlyList<Geometry.Vector2> volume_points = startingPrompt.Points;

                void callback(Polygon outputPolygon)
                {
                    UpdateLocationShapeFromVolumePolygon(outputPolygon);
                }

                // Launch segmentation command
                var channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                SegmentationCommand segmentCommand = new(
                    parent,
                    volume_points,
                    Array.Empty<Geometry.Vector2>(), // no background points initially
                    callback,
                    channelManager,
                    modelObj.Parent.TypeID,
                    modelObj.ID,
                    modelObj.ParentID,
                    startingBoxes: [startingPrompt.Seed]);

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
        /// Resegment this polygon. Clicks are the unsmoothed mosaic triangle centroids, the same set linked placement sends.
        /// </summary>
        protected void ContextMenu_SegmentPolygon(object sender, EventArgs e)
        {
            try
            {
                //ContextMenu menu = sender as ContextMenu;
                if (this.modelObj.TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON &&
                   this.modelObj.TypeCode != Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON)
                    return;

                var parent = AnnotationOverlay.CurrentOverlay?.Parent;
                if (parent is null)
                {
                    MessageBox.Show("There is no open section viewer to segment in.", "Segment", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Polygon controlRing = modelObj.MosaicShape.ToPolygon();
                IReadOnlyList<Geometry.Vector2> foregroundPoints =
                    PolygonSegmentationPrompts.CreateVolumeForegroundPoints(
                        controlRing,
                        parent.Section.ActiveSectionToVolumeTransform);

                void callback(Polygon volume_poly)
                {
                    UpdateLocationShapeFromVolumePolygon(volume_poly);
                }

                var channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                SegmentationCommand segmentCommand = new(
                    parent,
                    foregroundPoints,
                    Array.Empty<Geometry.Vector2>(), // no background points initially
                    callback,
                    channelManager,
                    modelObj.Parent.TypeID,
                    modelObj.ID,
                    modelObj.ParentID);

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
        /// Rasterizes this polygon and starts <see cref="SegmentationMaskEditCommand"/> on it so the mask can be grown and
        /// shrunk with the wheel. When the user accepts the edited mask it is sent to the segmentation server as the
        /// starting mask by a new <see cref="SegmentationCommand"/>, whose result the user accepts or declines; only an
        /// accepted result changes this location. Cancelling the edit changes nothing.
        /// </summary>
        /// <summary>Raster pixels left free around a polygon when it is rasterized for dilate/erode.</summary>
        private const int EditMaskMarginPixels = 128;

        protected void ContextMenu_EditPolygonMask(object sender, EventArgs e)
        {
            try
            {
                var parent = AnnotationOverlay.CurrentOverlay?.Parent;
                if (parent is null)
                {
                    MessageBox.Show("There is no open section viewer.", "Edit mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Polygon mosaicPolygon = modelObj.MosaicShape.ToPolygon();
                List<Geometry.Vector2[]> volumeRings = [];
                foreach (Geometry.Vector2[] ring in new[] { mosaicPolygon.ExteriorRing }.Concat(mosaicPolygon.InteriorRings))
                {
                    bool[] mapped = parent.Section.ActiveSectionToVolumeTransform.TrySectionToVolume(ring, out Geometry.Vector2[] volumeRing);
                    if (mapped.Any(ok => !ok))
                    {
                        MessageBox.Show("This polygon could not be mapped onto the volume.", "Edit mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    volumeRings.Add(volumeRing);
                }

                // The raster has the resolution of the view (a mosaic pixel is at least one volume unit), whatever the segmentation tile level.
                int downsample = Math.Max(1, (int)Math.Ceiling(parent.Camera?.Downsample ?? parent.Downsample));

                // The raster is edited in memory and drawn as one texture, so only the GPU limits it: a polygon too big for the
                // texture is downsampled until it fits. The server's limits apply when the mask is sent (SubmitEditedMask).
                Geometry.Rectangle polygonBounds = RasterMask.BoundsOf(volumeRings[0]);
                SegmentationDiag.Log($"EditMask start ds={downsample} polygon={polygonBounds.Width:0}x{polygonBounds.Height:0} maxTexture={parent.MaxTextureSide}");
                RasterMask mask = RasterMask.FromRings(
                    volumeRings, downsample, marginPixels: EditMaskMarginPixels, maxRasterSide: parent.MaxTextureSide);
                SegmentationDiag.Log(mask is null
                    ? "EditMask raster is null: the polygon covers no pixel centre"
                    : $"EditMask raster {mask.Width}x{mask.Height} scale={mask.Scale}");

                if (mask is null)
                {
                    MessageBox.Show("This polygon is too small to make a mask.", "Edit mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                parent.CurrentCommand = new SegmentationMaskEditCommand(parent, mask, edited =>
                {
                    if (edited is null)
                    {
                        Trace.WriteLine("Mask edit cancelled");
                        return;
                    }

                    Trace.WriteLine($"Mask edit accepted at downsample {edited.Downsample}: {edited.Pixels.Count(p => p != 0)} pixels");

                    // The edit command finishes itself right after this callback returns, so start the next one afterwards.
                    parent.BeginInvoke(() => SubmitEditedMask(parent, edited));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching mask edit: {ex.Message}");
                SegmentationDiag.Log($"EditMask failed: {ex}");
                MessageBox.Show($"Failed to launch mask edit: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Starts a <see cref="SegmentationCommand"/> that sends <paramref name="edited"/> to the segmentation server as
        /// the starting mask. The user accepts or declines the resulting segmentation; accepting replaces this location's
        /// shape, declining leaves it as it was.
        /// </summary>
        private void SubmitEditedMask(Viking.UI.Controls.SectionViewerControl parent, RasterMask edited)
        {
            try
            {
                SegmentationSeed? seed = SegmentationSeed.FromMask(edited);
                if (seed is null || !edited.TryGetSetWorldBounds(out Geometry.Rectangle setBounds))
                {
                    MessageBox.Show("The edited mask is empty, so there is nothing to segment from.", "Edit mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!Global.IsSegmentationServiceAvailable)
                {
                    MessageBox.Show("There is no segmentation service to send the edited mask to.", "Edit mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // The mask reaches the server as grid tiles, and the server accepts at most MaxForegroundTiles of them.
                long spanned = SegmentationMaskTiles.TilesSpanned(setBounds, edited.Downsample);
                if (spanned > SegmentationMaskTiles.MaxForegroundTiles)
                {
                    MessageBox.Show(
                        $"The edited mask spans {spanned} tiles. The segmentation server accepts a mask of at most {SegmentationMaskTiles.MaxForegroundTiles} tiles (about {(int)Math.Sqrt(SegmentationMaskTiles.MaxForegroundTiles) * SegmentationTileGrid.TileSize} pixels across).",
                        "Edit mask", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // The bitmap itself is the foreground mask; one click inside it tells the server which object is meant.
                var channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                parent.CurrentCommand = new SegmentationCommand(
                    parent,
                    [seed.Value.Center],
                    Array.Empty<Geometry.Vector2>(),
                    UpdateLocationShapeFromVolumePolygon,
                    channelManager,
                    modelObj.Parent.TypeID,
                    modelObj.ID,
                    modelObj.ParentID,
                    startingBoxes: [seed.Value]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error submitting edited mask: {ex.Message}");
                MessageBox.Show($"Failed to segment the edited mask: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private void UpdateLocationShapeFromVolumePolygon(Polygon volume_poly)
        {
            LocationShapeUpdate.ApplyVolumePolygon(modelObj, volume_poly, AnnotationOverlay.CurrentOverlay.Parent);
        }

        protected void ContextMenu_OnOffEdge(object sender, EventArgs e)
        {
            bool oldTerminal = modelObj.Terminal;
            bool oldOffEdge = modelObj.OffEdge;
            modelObj.OffEdge = !modelObj.OffEdge;
            try
            {
                Store.Locations.Save();
                UndoManager.PushLocationFlags("Toggle OffEdge", modelObj, oldTerminal, oldOffEdge);
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
        public int Section => modelObj.Section;

        /// <summary>
        /// Return true if the locations volume position has been calculated
        /// </summary>
        public bool VolumePositionHasBeenCalculated => modelObj.VolumePositionHasBeenCalculated;

        #endregion

    }
}
