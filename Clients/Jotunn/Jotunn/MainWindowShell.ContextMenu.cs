using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace Jotunn
{
    /// <summary>
    /// Viking's right-click menu on an annotation, for the actions Jotunn supports: the structure's property sheet, the
    /// terminal and off-edge flags, copying IDs, centering the view, and delete.
    /// </summary>
    public partial class MainWindow
    {
        void OnAnnotationContextMenuRequested(object sender, Geometry.Vector2 screen)
        {
            if (_sectionGrid?.SceneHostControl is Viking.VolumeView.SectionSceneHost host && TryShowProposalMenu(host.ScreenToWorld(screen)))
                return;

            LocationObj location = _tools?.LocationAt(screen);
            if (location == null)
                return;

            ContextMenu menu = BuildLocationMenu(location);
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        ContextMenu BuildLocationMenu(LocationObj location)
        {
            ContextMenu menu = new();
            string typeName = location.Parent?.Type?.Name;
            menu.Items.Add(new MenuItem
            {
                Header = typeName != null ? $"{typeName} {location.ParentID}, location {location.ID}" : $"Location {location.ID}",
                IsEnabled = false,
            });
            menu.Items.Add(new Separator());

            menu.Items.Add(Item("_Properties…", () => ShowStructureProperties(location), isEnabled: location.ParentID.HasValue));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("_Terminal", () => _tools.ToggleTerminal(location), isChecked: location.Terminal));
            menu.Items.Add(Item("_Off edge", () => _tools.ToggleOffEdge(location), isChecked: location.OffEdge));
            menu.Items.Add(ShapeMenu(location));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Copy _location ID", () => Clipboard.SetText(location.ID.ToString())));
            menu.Items.Add(Item("Copy _structure ID", () => Clipboard.SetText(location.ParentID.ToString()), isEnabled: location.ParentID.HasValue));
            menu.Items.Add(Item("_Center view here", () => GoToLocation(location)));
            if (WebAnnotation.Global.Export is WebAnnotation.Export export && location.ParentID is long exportId)
                menu.Items.Add(ExportMenu(export, exportId));
            if (WebAnnotation.SbfsemToolsLauncher.GetRootStructureIdForLocation(location) is long cellId
                && !string.IsNullOrWhiteSpace(WebAnnotation.SbfsemToolsLauncher.ResolveVolumeName()))
                menu.Items.Add(Item("Open in SBFSEM-_tools", () => WebAnnotation.SbfsemToolsLauncher.TryOpen(cellId, location.ID)));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("_Merge another structure into this one…", () => ShowMergeStructures(location.ParentID ?? 0), isEnabled: location.ParentID.HasValue));
            menu.Items.Add(SplitMenu(location));
            if (location.ParentID.HasValue)
                menu.Items.Add(UnmarkedTerminalsMenu(location.ParentID.Value));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("_Delete…", () => ConfirmAndDelete(location)));
            return menu;
        }

        /// <summary>Viking's Export > Tulip URL submenu for the location's structure.</summary>
        static MenuItem ExportMenu(WebAnnotation.Export export, long structureId)
        {
            MenuItem network = new() { Header = "_Network" };
            network.Items.Add(Item("_1 degree of separation", () => export.OpenNetwork(structureId, 1)));
            network.Items.Add(Item("_2 degrees of separation", () => export.OpenNetwork(structureId, 2)));
            network.Items.Add(Item("_3 degrees of separation", () => export.OpenNetwork(structureId, 3)));
            network.Items.Add(Item("_All connected", () => export.OpenNetwork(structureId, null)));

            MenuItem tulip = new() { Header = "Tulip URL" };
            tulip.Items.Add(Item("_Morphology", () => export.OpenMorphology(structureId)));
            tulip.Items.Add(network);

            MenuItem root = new() { Header = "E_xport" };
            root.Items.Add(tulip);
            return root;
        }

        /// <summary>Viking's "Change Shape" submenu, plus "Simplify Shape" for polygons.</summary>
        MenuItem ShapeMenu(LocationObj location)
        {
            MenuItem shape = new() { Header = "Change s_hape" };
            // Shape changes map through this section's transform, so they apply only to annotations on this section
            // (the menu also opens on faded annotations from adjacent sections).
            int viewSection = _sectionGrid?.SceneHostControl?.SectionNumber ?? 0;
            if ((int)Math.Round(location.Z) != viewSection)
            {
                shape.IsEnabled = false;
                shape.ToolTip = "Go to this annotation's section to change its shape";
                return shape;
            }

            bool circle = location.TypeCode == LocationType.CIRCLE;
            bool polygon = location.TypeCode is LocationType.POLYGON or LocationType.CURVEPOLYGON;
            if (!circle)
                shape.Items.Add(Item("_Circle", () => _tools.ConvertToCircle(location)));
            if (circle)
            {
                shape.Items.Add(Item("Convert to _polygon", () => _tools.ConvertCircleToPolygon(location)));
                shape.Items.Add(Item("_Segment to polygon…", () => SegmentCircle(location)));
            }

            if (polygon)
            {
                shape.Items.Add(Item("_Resegment…", () => SegmentCircle(location)));
                shape.Items.Add(Item("Si_mplify shape", () => _tools.SimplifyPolygon(location)));
                shape.Items.Add(Item("Random c_olor", () => _tools.RandomizeColor(location)));
            }

            return shape;
        }

        /// <summary>
        /// Viking splits a structure from a location link's menu; here each link of the location is listed, and the part of
        /// the structure on the far side of the chosen link becomes a new structure.
        /// </summary>
        MenuItem SplitMenu(LocationObj location)
        {
            MenuItem split = new() { Header = "_Split structure at link to" };
            foreach (long linked in location.LinksCopy)
                split.Items.Add(Item($"Location {linked}…", () => ShowSplitStructure(location.ID, linked)));
            split.IsEnabled = split.Items.Count > 0;
            return split;
        }

        /// <summary>
        /// Viking's "Unmarked process terminals": the structure's branch ends that are not marked terminal or off edge,
        /// by section, loaded from the server when the submenu opens; choosing one goes to it.
        /// </summary>
        MenuItem UnmarkedTerminalsMenu(long structureId)
        {
            MenuItem root = new() { Header = "Unmarked process _terminals" };
            root.Items.Add(new MenuItem { Header = "Loading…", IsEnabled = false });
            bool loaded = false;
            root.SubmenuOpened += async (_, _) =>
            {
                if (loaded)
                    return;
                loaded = true;

                LocationPositionOnly[] ends;
                try
                {
                    ends = await Store.Structures.GetUnfinishedBranchesWithPosition(structureId);
                }
                catch (Exception ex)
                {
                    root.Items.Clear();
                    root.Items.Add(new MenuItem { Header = $"Could not load: {ex.Message}", IsEnabled = false });
                    return;
                }

                root.Items.Clear();
                foreach (var section in (ends ?? []).GroupBy(e => (long)e.Position.Z).OrderBy(g => g.Key))
                {
                    MenuItem sectionItem = new() { Header = section.Key.ToString("D4") };
                    foreach (LocationPositionOnly end in section)
                    {
                        long id = end.ID;
                        sectionItem.Items.Add(Item($"Radius {end.Radius:F1}  X {end.Position.X:F0}  Y {end.Position.Y:F0}",
                            async () => GoToLocation(await Store.Locations.GetObjectByID(id))));
                    }

                    root.Items.Add(sectionItem);
                }

                if (root.Items.Count == 0)
                    root.Items.Add(new MenuItem { Header = "None", IsEnabled = false });
            };
            return root;
        }

        void ShowMergeStructures(long keepId)
        {
            using WebAnnotation.UI.MergeStructuresForm form = new() { KeepID = keepId };
            form.ShowDialog();
            _sectionGrid?.SceneHostControl?.Invalidate();
        }

        void ShowSplitStructure(long keepLocationId, long splitLocationId)
        {
            using WebAnnotation.UI.SplitStructuresForm form = new() { KeepID = keepLocationId, SplitID = splitLocationId };
            form.ShowDialog();
            _sectionGrid?.SceneHostControl?.Invalidate();
        }

        void OnMergeStructures(object sender, RoutedEventArgs e) => ShowMergeStructures(0);

        /// <summary>Viking's F6: the favorite structure types pen strokes offer to create.</summary>
        void OnChooseFavoriteStructureTypes(object sender, RoutedEventArgs e) =>
            WebAnnotation.AnnotationMenu.OnChooseFavoriteStructureTypes(sender, EventArgs.Empty);

        void OnTogglePenModeMenu(object sender, RoutedEventArgs e)
        {
            _tools?.TogglePenMode();
            PenModeMenuItem.IsChecked = WebAnnotation.Global.PenMode;
        }

        /// <summary>Viking's "Randomize All Colors": new display colors for every loaded cell.</summary>
        void OnRandomizeAllColors(object sender, RoutedEventArgs e)
        {
            int recolored = WebAnnotation.UI.StructureColors.RandomizeAll();
            _status?.SetMessage($"Recolored {recolored} cells");
            _sectionGrid?.SceneHostControl?.Invalidate();
        }

        void OnExportMotifs(object sender, RoutedEventArgs e)
        {
            if (WebAnnotation.Global.Export is WebAnnotation.Export export)
                export.OpenMotif();
            else
                _status?.SetMessage("This volume has no export service");
        }

        /// <summary>Viking's choice buttons after a pen stroke, as a menu at the pointer.</summary>
        void ShowChoices(System.Collections.Generic.IReadOnlyList<WebAnnotation.Tools.AnnotationChoice> choices)
        {
            ContextMenu menu = new() { Placement = PlacementMode.MousePoint };
            foreach (WebAnnotation.Tools.AnnotationChoice choice in choices)
                menu.Items.Add(Item(choice.Label.Replace("_", "__"), choice.Execute));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Do nothing", () => { }));
            menu.IsOpen = true;
        }

        static MenuItem Item(string header, Action click, bool isEnabled = true, bool? isChecked = null)
        {
            MenuItem item = new() { Header = header, IsEnabled = isEnabled };
            if (isChecked.HasValue)
            {
                item.IsCheckable = true;
                item.IsChecked = isChecked.Value;
            }

            item.Click += (_, _) => click();
            return item;
        }

        /// <summary>Viking's property sheet for the location's structure (general, notes, locations, child structures).</summary>
        async void ShowStructureProperties(LocationObj location)
        {
            try
            {
                if (location.ParentID is not long parentId)
                    return;

                StructureObj structure = await Store.Structures.GetObjectByID(parentId);
                if (structure == null)
                {
                    _status?.SetMessage($"Structure {parentId} could not be loaded");
                    return;
                }

                Viking.Common.ExtensionManager.RegisterPropertyPages(typeof(WebAnnotation.ViewModel.Structure).Assembly);
                Viking.Common.ExtensionManager.RegisterContextMenuProviders(typeof(WebAnnotation.ViewModel.Structure).Assembly);
                Viking.UI.Forms.PropertySheetForm.ShowDialog(new WebAnnotation.ViewModel.Structure(structure), null);
                _sectionGrid?.SceneHostControl?.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not show the structure's properties: {ex.Message}", "Jotunn", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
