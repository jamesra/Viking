using Jotunn.Common;
using Jotunn.Controls;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Viking.UI.WPF.Forms;
using Viking.VolumeModel;
using Viking.VolumeView;
using WebAnnotation;
using WebAnnotation.Tools;
using WebAnnotation.UI.Controls;
using WebAnnotation.UI.Forms;
using WebAnnotation.WPF.Forms;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;
using SectionVM = Viking.VolumeViewModel.SectionViewModel;

namespace Jotunn
{
    public partial class MainWindow : Window, IShellView
    {
        private SectionGridControl _sectionGrid;
        private AnnotationToolHost _tools;
        private MousePositionStatus _status;
        private BookmarksWindow _bookmarksWindow;

        public MainWindow()
        {
            SOTC_BindingErrorTracer.BindingErrorTraceListener.SetTrace();
            InitializeComponent();
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDownPreview));
        }

        public void AttachVolume(VolumeVM volume)
        {
            DataContext = volume;

            _sectionGrid = new SectionGridControl { DataContext = volume };
            ViewHost.Content = _sectionGrid;

            NavigationHost.Items.Clear();
            NavigationHost.Items.Add(new SectionList { DataContext = volume });

            _status = new MousePositionStatus();
            StatusHost.Content = _status;
            _sectionGrid.StatusDisplay = _status;

            Viking.VolumeModel.Volume model = volume.Volume;
            if (model != null)
                _sectionGrid.SceneHostControl.Volume = model;

            TryAttachAnnotations();
        }

        /// <summary>
        /// Wires AnnotationScene after Store.InitializeAsync. Safe to call from splash while tiles already draw.
        /// </summary>
        public void TryAttachAnnotations()
        {
            if (_sectionGrid?.SceneHostControl == null || !Store.IsInitialized)
                return;
            if (_sectionGrid.SceneHostControl.Annotations != null)
                return;

            Viking.VolumeModel.Volume model = (_sectionGrid.DataContext as VolumeVM)?.Volume;
            if (model == null)
                return;

            VolumeVM volume = (VolumeVM)_sectionGrid.DataContext;
            AnnotationScene scene = new(model);
            _sectionGrid.SceneHostControl.Annotations = scene;
            _tools = new AnnotationToolHost(_sectionGrid.SceneHostControl, scene);
            _tools.GoToRequested += (_, loc) => GoToLocation(loc);
            AnnotationOverlay.GoToLocationHandler = loc => Dispatcher.BeginInvoke(new Action(() => GoToLocation(loc)));
            _tools.StatusChanged += (_, text) => _status?.SetMessage(text);
            _tools.ChoicesProposed += (_, choices) => ShowChoices(choices);
            _sectionGrid.SceneHostControl.AnnotationTools = _tools;
            _sectionGrid.SceneHostControl.AnnotationContextMenuRequested += OnAnnotationContextMenuRequested;

            StructureTypeTree typeTree = new();
            typeTree.StructureTypeSelected += (_, id) => _tools.SelectedStructureTypeId = (long)id;
            NavigationHost.Items.Add(new TabItem { Header = "Types", Content = typeTree });
            _tools.UserName = App.UserCredentials?.UserName;
            _ = LoadHotkeysAsync(_tools);
            StartAnnotationPoll(scene);
            _ = AddReviewFeedTabAsync();
            string segmentationUrl = SegmentationServiceUrl();
            if (!string.IsNullOrWhiteSpace(segmentationUrl))
                _tools.SegmentationChannels = SegmentationChannels(segmentationUrl);
            AttachAutoPolygonize(_sectionGrid.SceneHostControl, scene, _tools);
            PenModeMenuItem.IsChecked = WebAnnotation.Global.PenMode;
            ExportMotifsMenuItem.IsEnabled = WebAnnotation.Global.Export != null;
            _sectionGrid.SceneHostControl.Invalidate();
        }

        System.Windows.Threading.DispatcherTimer _annotationPollTimer;

        /// <summary>Viking's idle poll: picks up other users' edits on the visible sections every 30 seconds.</summary>
        void StartAnnotationPoll(AnnotationScene scene)
        {
            _annotationPollTimer?.Stop();
            _annotationPollTimer = new System.Windows.Threading.DispatcherTimer { Interval = AnnotationScene.PollInterval };
            _annotationPollTimer.Tick += (_, _) =>
            {
                if (_sectionGrid?.SceneHostControl is SectionSceneHost host)
                    scene.PollSections(host.VisibleSectionNumbers);
            };
            _annotationPollTimer.Start();
        }

        /// <summary>
        /// Loads the volume's UserSettings shortcuts (Viking's structure and tag hotkeys), cached per volume under
        /// LocalApplicationData/Jotunn.
        /// </summary>
        static async Task LoadHotkeysAsync(AnnotationToolHost tools)
        {
            Viking.VolumeModel.Volume volume = WebAnnotation.AnnotationBootstrap.Volume;
            if (volume?.VolumeElement == null)
                return;

            string cacheDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jotunn", SafeFolderName(volume.Name), "WebAnnotation");
            tools.Hotkeys = await UserSettingsHotkeys.LoadAsync(volume.VolumeElement, cacheDirectory).ConfigureAwait(true);
        }

        static string SafeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Volume";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>
        /// Viking's key map for the view: + and - step sections (Shift for 10), PageUp/PageDown zoom, arrows pan a
        /// quarter view, Home snaps the zoom to a power of two, Backspace returns to the last edited annotation, F5
        /// reloads annotations, Delete deletes the selected annotation, Enter commits a tool or continues the last
        /// trace, Ctrl+C copies the cursor position.
        /// </summary>
        protected void OnKeyDownPreview(object sender, KeyEventArgs e)
        {
            if (IsTextInputTarget(e.OriginalSource as DependencyObject))
                return;

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            SectionSceneHost host = _sectionGrid?.SceneHostControl;

            if (_tools != null && !_tools.IsIdle && (e.Key == Key.Back || e.Key == Key.Tab))
            {
                // A tool in progress owns Backspace (remove the last point); Tab must not move focus off the view.
                if (e.Key == Key.Back)
                    _tools.OnKeyDown(Key.Back);
                e.Handled = true;
                return;
            }

            if (!ctrl && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
                StepSections(shift ? 10 : 1);
                e.Handled = true;
            }
            else if (!ctrl && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
            {
                StepSections(shift ? -10 : -1);
                e.Handled = true;
            }
            else if (e.Key == Key.Insert)
            {
                StepSections(1);
                e.Handled = true;
            }
            else if (host != null && !ctrl && (e.Key == Key.PageUp || e.Key == Key.PageDown))
            {
                host.StepMagnification(zoomIn: e.Key == Key.PageUp);
                e.Handled = true;
            }
            else if (host != null && !ctrl && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                host.PanByViewFraction(
                    e.Key == Key.Left ? -0.25 : e.Key == Key.Right ? 0.25 : 0,
                    e.Key == Key.Up ? 0.25 : e.Key == Key.Down ? -0.25 : 0);
                e.Handled = true;
            }
            else if (host != null && e.Key == Key.Home)
            {
                host.SnapMagnificationToPowerOfTwo();
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                _ = GoToLastEditedAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                ReloadAnnotations();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                GlobalCommands.DeleteAnnotation.Execute(null, this);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.C)
            {
                CopyPointerPosition();
                e.Handled = true;
            }
            else if (_tools != null && !ctrl && (e.Key == Key.Multiply || (shift && e.Key == Key.D8)))
            {
                _tools.TogglePenMode();
                PenModeMenuItem.IsChecked = WebAnnotation.Global.PenMode;
                e.Handled = true;
            }
            else if (!ctrl && e.Key == Key.F6)
            {
                OnChooseFavoriteStructureTypes(this, e);
                e.Handled = true;
            }
            else if (!ctrl && !shift && e.Key == Key.Tab && _tools != null && _tools.IsIdle)
            {
                StartSegmentation();
                e.Handled = true;
            }
            else if (ctrl && !shift && e.Key == Key.Z)
            {
                GlobalCommands.UndoAnnotationEdit.Execute(null, this);
                e.Handled = true;
            }
            else if (e.Key == Key.Space)
            {
                GlobalCommands.HideAnnotations.Execute(null, this);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                GlobalCommands.CancelTool.Execute(null, this);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (_tools != null && !_tools.IsIdle)
                    GlobalCommands.CommitTool.Execute(null, this);
                else
                    GlobalCommands.ContinueLast.Execute(null, this);
                e.Handled = true;
            }
            else if (_sectionGrid?.SceneHostControl != null && _sectionGrid.SceneHostControl.HandleViewKeyDown(e.Key))
            {
                e.Handled = true;
            }
            else if (_tools != null && _tools.TryRunHotkey(e.Key == Key.System ? e.SystemKey : e.Key, shift, ctrl,
                         (Keyboard.Modifiers & ModifierKeys.Alt) != 0))
            {
                e.Handled = true;
            }
        }

        static bool IsTextInputTarget(DependencyObject source)
        {
            while (source != null)
            {
                if (source is TextBox || source is ComboBox)
                    return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }

        void GoToLocation(LocationObj loc)
        {
            if (loc == null || DataContext is not VolumeVM volume || volume.VisibleRegion == null)
                return;

            System.Windows.Rect region = volume.VisibleRegion.VisibleRect;
            double width = Math.Max(region.Width, 1);
            double height = Math.Max(region.Height, 1);
            volume.VisibleRegion = new VisibleRegionInfo(
                new System.Windows.Rect(loc.VolumePosition.X - width / 2, loc.VolumePosition.Y - height / 2, width, height),
                volume.VisibleRegion.Downsample);
            int z = (int)Math.Round(loc.Z);
            if (volume.SectionViewModels.ContainsKey(z))
                volume.CenterIndex = volume.SectionViewModels.IndexOfKey(z);
        }

        private void OnIncrementSectionNumber(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is VolumeVM volume)
                volume.CenterIndex++;
        }

        private void OnDecrementSectionNumber(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is VolumeVM volume)
                volume.CenterIndex--;
        }

        private void OnAddGridRow(object sender, ExecutedRoutedEventArgs e)
        {
            VirtualizingGrid grid = FindSectionGrid();
            if (grid != null)
                grid.NumRows += 2;
        }

        private void OnRemoveGridRow(object sender, ExecutedRoutedEventArgs e)
        {
            VirtualizingGrid grid = FindSectionGrid();
            if (grid != null && grid.NumRows > 1)
                grid.NumRows = Math.Max(1, grid.NumRows - 2);
        }

        private void OnAddGridColumn(object sender, ExecutedRoutedEventArgs e)
        {
            VirtualizingGrid grid = FindSectionGrid();
            if (grid != null)
                grid.NumCols += 2;
        }

        private void OnRemoveGridColumn(object sender, ExecutedRoutedEventArgs e)
        {
            VirtualizingGrid grid = FindSectionGrid();
            if (grid != null && grid.NumCols > 1)
                grid.NumCols = Math.Max(1, grid.NumCols - 2);
        }

        private VirtualizingGrid FindSectionGrid()
        {
            return _sectionGrid?.GridPanel;
        }

        void OnGoToLocation(object sender, ExecutedRoutedEventArgs e)
        {
            GoToActionForm form = new()
            {
                Owner = this,
                Title = "Go to location",
                IsValidInput = async (id, token) =>
                {
                    LocationObj loc = await Store.Locations.GetObjectByID(id, token);
                    return loc != null;
                },
                OnGo = id => _ = GoToLocationByIdAsync(id)
            };
            form.Show();
        }

        async Task GoToLocationByIdAsync(long id)
        {
            LocationObj loc = await Store.Locations.GetObjectByID(id);
            GoToLocation(loc);
        }

        void OnFindStructure(object sender, ExecutedRoutedEventArgs e)
        {
            FindStructureNumberForm form = new()
            {
                Owner = this,
                OnFindStructure = async id =>
                {
                    StructureObj structure = await Store.Structures.GetObjectByID(id);
                    if (structure == null)
                    {
                        MessageBox.Show("No structure found with that ID", "Jotunn", MessageBoxButton.OK, MessageBoxImage.Information);
                        return false;
                    }

                    LocationObj[] locals = Store.Locations.GetLocalObjectsForStructure(id);
                    LocationObj loc = locals?.FirstOrDefault();
                    if (loc == null)
                    {
                        MessageBox.Show($"Structure {id} loaded but has no locations in memory yet.", "Jotunn", MessageBoxButton.OK, MessageBoxImage.Information);
                        return true;
                    }

                    GoToLocation(loc);
                    return true;
                }
            };
            form.Show();
        }

        /// <summary>Continues the last trace at the cursor, as Viking does, or at the view center when the cursor is elsewhere.</summary>
        void OnContinueLast(object sender, ExecutedRoutedEventArgs e)
        {
            if (_tools == null || DataContext is not VolumeVM volume || volume.VisibleRegion == null)
                return;

            SectionSceneHost host = _sectionGrid?.SceneHostControl;
            Geometry.Vector2 world = host != null && host.TryGetPointerWorldPosition(out Geometry.Vector2 pointer, out _)
                ? pointer
                : new(volume.VisibleRegion.Center.X, volume.VisibleRegion.Center.Y);
            _tools.ContinueLast(world);
        }

        /// <summary>Deletes the selected annotation (else the last edited one) after the user confirms.</summary>
        void OnDeleteAnnotation(object sender, ExecutedRoutedEventArgs e)
        {
            LocationObj target = _tools?.DeletionTarget;
            if (target == null)
            {
                _status?.SetMessage("Click an annotation to select it before deleting");
                return;
            }

            ConfirmAndDelete(target);
        }

        void ConfirmAndDelete(LocationObj target)
        {
            if (MessageBox.Show(this, $"Delete location {target.ID} of structure {target.ParentID}?", "Jotunn",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                _tools.DeleteLocation(target);
            }
        }

        void StepSections(int count)
        {
            if (DataContext is not VolumeVM volume || volume.SectionViewModels.Count == 0)
                return;

            volume.CenterIndex = Math.Max(0, Math.Min(volume.SectionViewModels.Count - 1, volume.CenterIndex + count));
        }

        void OnUndoAnnotationEdit(object sender, ExecutedRoutedEventArgs e) => _tools?.Undo();

        void OnGoToLastEdited(object sender, RoutedEventArgs e) => _ = GoToLastEditedAsync();

        /// <summary>Viking's Backspace: the last annotation edited in this session, else the volume's last modified location.</summary>
        async Task GoToLastEditedAsync()
        {
            if (!Store.IsInitialized)
                return;

            LocationObj loc = WebAnnotation.Global.LastEditedAnnotationID.HasValue
                ? await Store.Locations.GetObjectByID(WebAnnotation.Global.LastEditedAnnotationID.Value)
                : await Store.Locations.GetLastModifiedLocation();
            if (loc == null)
            {
                _status?.SetMessage("No edited annotation to return to");
                return;
            }

            GoToLocation(loc);
        }

        void OnReloadAnnotations(object sender, RoutedEventArgs e) => ReloadAnnotations();

        void ReloadAnnotations()
        {
            (_sectionGrid?.SceneHostControl?.Annotations as AnnotationScene)?.Reload();
            _status?.SetMessage("Reloading annotations");
        }

        /// <summary>Viking's Ctrl+C: copies the cursor's X, Y, section and downsample in Viking's paste format.</summary>
        void CopyPointerPosition()
        {
            SectionSceneHost host = _sectionGrid?.SceneHostControl;
            if (host == null || !host.TryGetPointerWorldPosition(out Geometry.Vector2 world, out int section))
                return;

            Clipboard.SetText(Viking.Common.Util.CoordinatesToCopyPaste(world.X, world.Y, section, host.Downsample));
            _status?.SetMessage("Position copied");
        }

        void OnHideAnnotations(object sender, ExecutedRoutedEventArgs e) => _tools?.ToggleAnnotationsVisible();

        void OnCommitTool(object sender, ExecutedRoutedEventArgs e) => _tools?.CommitCurrent();

        void OnCancelTool(object sender, ExecutedRoutedEventArgs e) => _tools?.CancelCurrent();

        void OnPlaceCircle(object sender, ExecutedRoutedEventArgs e) => _tools?.ArmPlace(AnnotationPlaceKind.Circle);

        void OnPlacePolyline(object sender, ExecutedRoutedEventArgs e) => _tools?.ArmPlace(AnnotationPlaceKind.Polyline);

        void OnPlacePolygon(object sender, ExecutedRoutedEventArgs e) => _tools?.ArmPlace(AnnotationPlaceKind.Polygon);

        void OnMeasureDistance(object sender, ExecutedRoutedEventArgs e) => _tools?.StartMeasure();

        void OnAnnotationPreferences(object sender, ExecutedRoutedEventArgs e)
        {
            AnnotationPreferencesDialogViewModel vm = new();
            vm.LoadCurrentSettings(
                WebAnnotation.Global.AnnotationSettings.NumSectionsInMemory,
                WebAnnotation.Global.AnnotationSettings.NumSectionsLoading,
                WebAnnotation.Global.AnnotationSettings.LocationTextScaleFactor,
                WebAnnotation.Global.AnnotationSettings.ReferenceLocationTextScaleFactor,
                WebAnnotation.Global.AnnotationSettings.DefaultClosedLineWidth,
                WebAnnotation.Global.AnnotationSettings.DefaultLocationJumpDownsample,
                WebAnnotation.Global.AnnotationSettings.AdjacentLocationRadiusScalar,
                WebAnnotation.Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay,
                WebAnnotation.Global.AnnotationSettings.PenSimplifyThreshold,
                WebAnnotation.Global.AnnotationSettings.MinRadius,
                WebAnnotation.Global.AnnotationSettings.PolygonOpacityParentless,
                WebAnnotation.Global.AnnotationSettings.PolygonOpacityWithParent,
                WebAnnotation.Global.AnnotationSettings.CircleOpacityParentless,
                WebAnnotation.Global.AnnotationSettings.CircleOpacityWithParent,
                WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius,
                WebAnnotation.Global.AnnotationSettings.PolygonPointDiameter,
                WebAnnotation.Global.AnnotationSettings.SmallestRenderedSize,
                WebAnnotation.Global.AnnotationSettings.PolygonVertexPointsVisibleAtWidthFraction,
                WebAnnotation.Global.AnnotationSettings.PolygonVertexPointsHiddenAtWidthFraction,
                correctionShimEnabled: WebAnnotation.Global.AnnotationSettings.CorrectionShimEnabled,
                correctionShimFadeStartDownsample: WebAnnotation.Global.AnnotationSettings.CorrectionShimFadeStartDownsample,
                correctionShimFadeEndDownsample: WebAnnotation.Global.AnnotationSettings.CorrectionShimFadeEndDownsample);
            AnnotationPreferencesDialog dialog = new(vm) { Owner = this };
            dialog.OkClicked += (_, _) => ApplyAnnotationPreferences(vm);
            dialog.ApplyClicked += (_, _) => ApplyAnnotationPreferences(vm);
            dialog.ResetToDefaultsRequested += (_, _) =>
            {
                WebAnnotation.Global.AnnotationSettings.ResetToDefaults();
                vm.ResetToDefaults();
            };
            dialog.Show();
        }

        static void ApplyAnnotationPreferences(AnnotationPreferencesDialogViewModel vm)
        {
            WebAnnotation.Global.AnnotationSettings.NumSectionsInMemory = vm.NumSectionsInMemory;
            WebAnnotation.Global.AnnotationSettings.NumSectionsLoading = vm.NumSectionsLoading;
            WebAnnotation.Global.AnnotationSettings.LocationTextScaleFactor = vm.LocationTextScaleFactor;
            WebAnnotation.Global.AnnotationSettings.ReferenceLocationTextScaleFactor = vm.ReferenceLocationTextScaleFactor;
            WebAnnotation.Global.AnnotationSettings.DefaultClosedLineWidth = vm.DefaultClosedLineWidth;
            WebAnnotation.Global.AnnotationSettings.DefaultLocationJumpDownsample = vm.DefaultLocationJumpDownsample;
            WebAnnotation.Global.AnnotationSettings.AdjacentLocationRadiusScalar = vm.AdjacentLocationRadiusScalar;
            WebAnnotation.Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay = vm.NumClosedCurveInterpolationPointsForDisplay;
            WebAnnotation.Global.AnnotationSettings.PenSimplifyThreshold = vm.PenSimplifyThreshold;
            WebAnnotation.Global.AnnotationSettings.MinRadius = vm.MinRadius;
            WebAnnotation.Global.AnnotationSettings.SegmentationPointRadius = vm.SegmentationPointRadius;
            WebAnnotation.Global.AnnotationSettings.PolygonPointDiameter = vm.PolygonPointDiameter;
            WebAnnotation.Global.AnnotationSettings.SmallestRenderedSize = vm.SmallestRenderedSize;
            WebAnnotation.Global.AnnotationSettings.PolygonVertexPointsVisibleAtWidthFraction = vm.PolygonVertexPointsVisibleAtWidthFraction;
            WebAnnotation.Global.AnnotationSettings.PolygonVertexPointsHiddenAtWidthFraction = vm.PolygonVertexPointsHiddenAtWidthFraction;
            WebAnnotation.Global.AnnotationSettings.PolygonOpacityParentless = (float)vm.PolygonOpacityParentless;
            WebAnnotation.Global.AnnotationSettings.PolygonOpacityWithParent = (float)vm.PolygonOpacityWithParent;
            WebAnnotation.Global.AnnotationSettings.CircleOpacityParentless = (float)vm.CircleOpacityParentless;
            WebAnnotation.Global.AnnotationSettings.CircleOpacityWithParent = (float)vm.CircleOpacityWithParent;
            WebAnnotation.Global.AnnotationSettings.CorrectionShimEnabled = vm.CorrectionShimEnabled;
            WebAnnotation.Global.AnnotationSettings.CorrectionShimFadeStartDownsample = vm.CorrectionShimFadeStartDownsample;
            WebAnnotation.Global.AnnotationSettings.CorrectionShimFadeEndDownsample = vm.CorrectionShimFadeEndDownsample;
            vm.Apply();
        }

        void OnViewerPreferences(object sender, ExecutedRoutedEventArgs e)
        {
            ViewerPreferencesDialog dialog = new() { Owner = this };
            dialog.Show();
        }

        void OnSetupChannels(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is not VolumeVM volume)
                return;

            SectionVM current = null;
            if (volume.SectionViewModels.Count > 0 && volume.CenterIndex >= 0 && volume.CenterIndex < volume.SectionViewModels.Count)
                current = volume.SectionViewModels.Values[volume.CenterIndex];

            ChannelSetupDialog dialog = new() { Owner = this };
            dialog.SetChannelData(
                current?.ChannelInfoArray ?? volume.DefaultChannels,
                volume.ChannelNames);
            if (dialog.ShowDialog() != true)
                return;

            ChannelInfo[] channels = dialog.Channels;
            volume.DefaultChannels = channels;
            if (current != null)
                current.ChannelInfoArray = channels;
            _sectionGrid?.SceneHostControl.Invalidate();
        }

        void OnManageStructureTypes(object sender, ExecutedRoutedEventArgs e)
        {
            StructureTypeManagementForm form = new()
            {
                Owner = this,
                DataContext = Store.StructureTypes
            };
            form.Show();
        }

        void OnBookmarks(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is not VolumeVM volume)
                return;
            if (_bookmarksWindow != null)
            {
                _bookmarksWindow.Activate();
                return;
            }

            _bookmarksWindow = new BookmarksWindow(volume.Name, BookmarkStore.Load(volume.Name))
            {
                Owner = this
            };
            _bookmarksWindow.GoToBookmark += GoToBookmark;
            _bookmarksWindow.RequestCurrentView += CurrentBookmark;
            _bookmarksWindow.Closed += (_, _) => _bookmarksWindow = null;
            _bookmarksWindow.Show();
        }

        void OnAddBookmark(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is not VolumeVM volume)
                return;
            BookmarkEntry entry = CurrentBookmark();
            if (entry == null)
                return;
            var list = BookmarkStore.Load(volume.Name);
            list.Add(entry);
            BookmarkStore.Save(volume.Name, list);
            _status?.SetMessage($"Bookmark added: {entry.Name}");
        }

        BookmarkEntry CurrentBookmark()
        {
            if (DataContext is not VolumeVM volume || volume.VisibleRegion == null)
                return null;
            int section = _sectionGrid?.SceneHostControl.SectionNumber ?? 0;
            return BookmarkStore.FromVisibleRegion(
                $"Section {section} ({DateTime.Now:HH:mm})",
                section,
                volume.VisibleRegion);
        }

        void GoToBookmark(BookmarkEntry entry)
        {
            if (DataContext is not VolumeVM volume || volume.VisibleRegion == null)
                return;
            Size area = new(volume.VisibleRegion.VisibleRect.Width, volume.VisibleRegion.VisibleRect.Height);
            volume.VisibleRegion = new VisibleRegionInfo(new Point(entry.X, entry.Y), area, entry.Downsample);
            if (volume.SectionViewModels.ContainsKey(entry.Section))
                volume.CenterIndex = volume.SectionViewModels.IndexOfKey(entry.Section);
        }

        void OnExportScreenshot(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "PNG image|*.png",
                FileName = "jotunn.png"
            };
            if (dialog.ShowDialog(this) != true)
                return;
            if (_sectionGrid.SceneHostControl.TryCaptureScreenshot(dialog.FileName))
                _status?.SetMessage("Screenshot saved");
            else
                MessageBox.Show("Could not capture the current view.", "Jotunn", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        void OnExportVisibleAnnotations(object sender, ExecutedRoutedEventArgs e)
        {
            if (_sectionGrid?.SceneHostControl == null)
                return;
            int section = _sectionGrid.SceneHostControl.SectionNumber;
            var locals = Store.Locations.GetLocalObjectsForSection(section);
            SaveFileDialog dialog = new()
            {
                Filter = "CSV|*.csv",
                FileName = $"annotations-s{section}.csv"
            };
            if (dialog.ShowDialog(this) != true)
                return;

            StringBuilder csv = new();
            csv.AppendLine("ID,ParentID,Type,X,Y,Z,Radius");
            foreach (LocationObj loc in locals.Values)
            {
                csv.AppendLine($"{loc.ID},{loc.ParentID},{loc.TypeCode},{loc.VolumePosition.X},{loc.VolumePosition.Y},{loc.Z},{loc.Radius}");
            }
            File.WriteAllText(dialog.FileName, csv.ToString());
            _status?.SetMessage($"Exported {locals.Count} annotations");
        }

        void OnSegmentation(object sender, ExecutedRoutedEventArgs e) => StartSegmentation();

        Viking.Services.Grpc.IGrpcChannelManager _segmentationChannels;

        /// <summary>Viking's Tab: segment a new structure with the segmentation service chosen at login.</summary>
        void StartSegmentation()
        {
            string url = SegmentationServiceUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                _status?.SetMessage("No segmentation service was chosen at login");
                return;
            }

            if (_tools == null || _sectionGrid?.SceneHostControl is not SectionSceneHost host)
                return;

            _tools.StartSegmentation(host, SegmentationChannels(url));
        }

        /// <summary>The segmentation service chosen at login, or null when there is none.</summary>
        /// <summary>
        /// The segmentation service in use: the one chosen with Tools > Select segmentation service (Viking's setting,
        /// which the login's choice also sets), else the login's.
        /// </summary>
        static string SegmentationServiceUrl() =>
            !string.IsNullOrWhiteSpace(WebAnnotation.Global.AnnotationSettings.SegmentationServiceUrl)
                ? WebAnnotation.Global.AnnotationSettings.SegmentationServiceUrl
                : App.SegmentationServiceUrl;

        string _segmentationChannelsUrl;

        /// <summary>Channels to <paramref name="url"/>, made again when the service changes.</summary>
        Viking.Services.Grpc.IGrpcChannelManager SegmentationChannels(string url)
        {
            if (_segmentationChannels == null || !string.Equals(_segmentationChannelsUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                _segmentationChannels = new Viking.Services.Grpc.GrpcChannelManager(new FixedSegmentationEndpoint(url));
                _segmentationChannelsUrl = url;
            }

            return _segmentationChannels;
        }

        /// <summary>
        /// Viking's "Select Segmentation Service…": lists the services Identity offers this user. Choosing one switches
        /// segmentation, the tools and auto-polygonize to it.
        /// </summary>
        void OnSelectSegmentationService(object sender, RoutedEventArgs e)
        {
            WebAnnotation.AnnotationMenu.OnSelectSegmentationService(sender, EventArgs.Empty);

            string url = SegmentationServiceUrl();
            if (_tools != null)
                _tools.SegmentationChannels = string.IsNullOrWhiteSpace(url) ? null : SegmentationChannels(url);
            ApplyAutoPolygonizeSetting();
        }

        /// <summary>Viking's "Segment to Polygon" on a circle, or "Resegment" on a polygon.</summary>
        void SegmentCircle(LocationObj circle)
        {
            string url = SegmentationServiceUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                _status?.SetMessage("No segmentation service was chosen at login");
                return;
            }

            if (_tools != null && _sectionGrid?.SceneHostControl is SectionSceneHost host)
                _tools.StartSegmentationOf(circle, host, SegmentationChannels(url));
        }

        sealed class FixedSegmentationEndpoint(string endpoint) : Viking.DependencyInjection.IGrpcServiceConfiguration
        {
            public string Endpoint() => endpoint;
        }

        void OnExit(object sender, RoutedEventArgs e) => Close();

        void IShellView.ShowView()
        {
            Show();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override GeometryHitTestResult HitTestCore(GeometryHitTestParameters hitTestParameters)
        {
            GeometryHitTestResult r = base.HitTestCore(hitTestParameters);
            if (r == null)
                return r;

            ContentControl control = r.VisualHit as ContentControl;
            if (control == null)
                return r;

            System.Diagnostics.Trace.WriteLine(control.Name + " was hit");
            return r;
        }

        protected override System.Windows.Media.HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            System.Windows.Media.HitTestResult r = base.HitTestCore(hitTestParameters);
            if (r == null)
                return r;

            ContentControl control = r.VisualHit as ContentControl;
            if (control == null)
                return r;

            System.Diagnostics.Trace.WriteLine(control.Name + " was hit");
            return r;
        }
    }
}
