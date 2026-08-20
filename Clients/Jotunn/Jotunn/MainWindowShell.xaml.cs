using Jotunn.Common;
using Jotunn.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Viking.VolumeView;
using WebAnnotation.UI.Controls;
using WebAnnotationModel;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;

namespace Jotunn
{
    public partial class MainWindow : Window, IShellView
    {
        private SectionGridControl _sectionGrid;

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

            MousePositionStatus status = new MousePositionStatus();
            StatusHost.Content = status;
            _sectionGrid.StatusDisplay = status;

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
            WebAnnotation.AnnotationScene scene = new(model);
            _sectionGrid.SceneHostControl.Annotations = scene;
            WebAnnotation.ViewportAnnotationController controller = new(_sectionGrid.SceneHostControl, scene);
            controller.GoToRequested += (_, loc) =>
            {
                if (loc == null || volume.VisibleRegion == null)
                    return;
                System.Windows.Rect region = volume.VisibleRegion.VisibleRect;
                double width = Math.Max(region.Width, 1);
                double height = Math.Max(region.Height, 1);
                volume.VisibleRegion = new Jotunn.Common.VisibleRegionInfo(
                    new System.Windows.Rect(loc.VolumePosition.X - width / 2, loc.VolumePosition.Y - height / 2, width, height),
                    volume.VisibleRegion.Downsample);
                int z = (int)Math.Round(loc.Z);
                if (volume.SectionViewModels.ContainsKey(z))
                    volume.CenterIndex = volume.SectionViewModels.IndexOfKey(z);
            };
            _sectionGrid.SceneHostControl.AnnotationController = controller;

            StructureTypeTree typeTree = new();
            typeTree.StructureTypeSelected += (_, id) => controller.SelectedStructureTypeId = (long)id;
            NavigationHost.Items.Add(new TabItem { Header = "Types", Content = typeTree });
            if (Store.StructureTypes.RootObjects.Count > 0)
                controller.SelectedStructureTypeId = (long)Store.StructureTypes.RootObjects[0];
            _sectionGrid.SceneHostControl.Invalidate();
        }

        protected void OnKeyDownPreview(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Insert)
            {
                GlobalCommands.IncrementSectionNumber.Execute(null, this);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                GlobalCommands.DecrementSectionNumber.Execute(null, this);
                e.Handled = true;
            }
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

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            HitTestResult r = base.HitTestCore(hitTestParameters);
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
