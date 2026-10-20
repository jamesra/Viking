using System;
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
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("_Merge another structure into this one…", () => ShowMergeStructures(location.ParentID ?? 0), isEnabled: location.ParentID.HasValue));
            menu.Items.Add(SplitMenu(location));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("_Delete…", () => ConfirmAndDelete(location)));
            return menu;
        }

        /// <summary>Viking's "Change Shape" submenu, plus "Simplify Shape" for polygons.</summary>
        MenuItem ShapeMenu(LocationObj location)
        {
            MenuItem shape = new() { Header = "Change s_hape" };
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
