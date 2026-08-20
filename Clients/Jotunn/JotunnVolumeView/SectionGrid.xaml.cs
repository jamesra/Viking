using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Geometry;
using Jotunn.Controls;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;

namespace Viking.VolumeView
{
    /// <summary>
    /// Stacks the MonoGame host, a transparent input overlay, and a non-hit-testable section-name grid.
    /// </summary>
    public partial class SectionGridControl : UserControl
    {
        public SectionGridControl()
        {
            InitializeComponent();
            DataContextChanged += OnVolumeDataContextChanged;
            Loaded += (_, _) =>
            {
                SceneHost.Grid = GridPanel;
                Dispatcher.BeginInvoke(new Action(() => SceneHost.Grid = GridPanel), System.Windows.Threading.DispatcherPriority.Loaded);
            };
            LayoutUpdated += (_, _) =>
            {
                if (SceneHost.Grid == null)
                    SceneHost.Grid = GridPanel;
            };
        }

        /// <summary>
        /// The MonoGame host in this control. The overlay routes mouse here; GridPanel is assigned after template apply.
        /// </summary>
        public SectionSceneHost SceneHostControl => SceneHost;

        /// <summary>
        /// Status strip updated from overlay mouse move. Assigned by MainWindow.AttachVolume.
        /// </summary>
        public MousePositionStatus StatusDisplay { get; set; }

        /// <summary>
        /// The VirtualizingGrid inside SectionsGrid's ItemsPanelTemplate. Walks the visual
        /// tree after ApplyTemplate; returns null until the presenter has a child.
        /// </summary>
        public VirtualizingGrid GridPanel
        {
            get
            {
                if (SectionsGrid == null)
                    return null;

                SectionsGrid.ApplyTemplate();
                ItemsPresenter presenter = FindVisualChild<ItemsPresenter>(SectionsGrid);
                if (presenter == null)
                    return null;

                presenter.ApplyTemplate();
                if (VisualTreeHelper.GetChildrenCount(presenter) == 0)
                    return null;

                return VisualTreeHelper.GetChild(presenter, 0) as VirtualizingGrid;
            }
        }

        void OnViewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.XButton1 && e.ChangedButton != MouseButton.XButton2)
                return;

            SceneHost.HandleViewMouseDown(e.GetPosition(SceneHost), e.ChangedButton, e.ClickCount);
            UpdateMouseStatus(e.GetPosition(SceneHost));
            e.Handled = true;
        }

        void OnViewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).CaptureMouse();
            SceneHost.HandleViewMouseDown(e.GetPosition(SceneHost), MouseButton.Left, e.ClickCount);
            UpdateMouseStatus(e.GetPosition(SceneHost));
            e.Handled = true;
        }

        void OnViewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).CaptureMouse();
            SceneHost.HandleViewMouseDown(e.GetPosition(SceneHost), MouseButton.Right, e.ClickCount);
            UpdateMouseStatus(e.GetPosition(SceneHost));
            e.Handled = true;
        }

        void OnViewMouseMove(object sender, MouseEventArgs e)
        {
            SceneHost.HandleViewMouseMove(e.GetPosition(SceneHost), e.LeftButton, e.RightButton);
            UpdateMouseStatus(e.GetPosition(SceneHost));
        }

        void OnViewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).ReleaseMouseCapture();
            SceneHost.HandleViewMouseUp(e.GetPosition(SceneHost), MouseButton.Left);
            UpdateMouseStatus(e.GetPosition(SceneHost));
            e.Handled = true;
        }

        void OnViewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).ReleaseMouseCapture();
            SceneHost.HandleViewMouseUp(e.GetPosition(SceneHost), MouseButton.Right);
            UpdateMouseStatus(e.GetPosition(SceneHost));
            e.Handled = true;
        }

        void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            SceneHost.HandleViewMouseWheel(e.Delta, e.GetPosition(SceneHost));
            UpdateMouseStatus(e.GetPosition(SceneHost));
            e.Handled = true;
        }

        void UpdateMouseStatus(Point hostPoint)
        {
            if (StatusDisplay == null)
                return;
            if (!SceneHost.TryGetWorldPosition(hostPoint, out Vector2 world, out int section))
                return;
            StatusDisplay.Update(section, world.X, world.Y, SceneHost.Downsample);
        }

        private void OnVolumeDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            VolumeVM volume = e.NewValue as VolumeVM;
            if (volume != null)
                SectionsGrid.ItemsSource = volume.SectionViewModels.Values;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;

                T nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
