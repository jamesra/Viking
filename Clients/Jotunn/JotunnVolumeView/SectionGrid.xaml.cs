using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Jotunn.Controls;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;

namespace Viking.VolumeView
{
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

        public SectionSceneHost SceneHostControl => SceneHost;

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
