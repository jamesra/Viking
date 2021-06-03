using System.Windows.Controls;

namespace Viking.VolumeView
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class SectionGridControl : UserControl
    {
        /*
        private static RoutedUICommand incrementCenterIndexCommand;
        private static RoutedUICommand decrementCenterIndexCommand;

        /// <summary>
        /// Increment the center number
        /// </summary>
        public static RoutedUICommand IncrementCommand
        {
            get { return incrementCenterIndexCommand; }
        }

        /// <summary>
        /// Decrement the center number
        /// </summary>
        public static RoutedUICommand DecrementCommand
        {
            get { return decrementCenterIndexCommand; }
        }
        */

        public SectionGridControl()
        {
            InitializeComponent();

            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();

            this.DataContext = volume;
            SectionsGrid.ItemsSource = volume.SectionViewModels.Values;

            /*
            incrementCenterIndexCommand = new RoutedUICommand("+", "IncrementCenterIndexCommand", typeof(SectionGridControl));
            decrementCenterIndexCommand = new RoutedUICommand("-", "DecrementCenterIndexCommand", typeof(SectionGridControl));

            CommandBinding cb = new CommandBinding(incrementCenterIndexCommand, OnIncrementSectionNumber);
            this.CommandBindings.Add(cb);

            GlobalCommands.IncrementSectionNumber.RegisterCommand(incrementCenterIndexCommand);
             */
        }

        /*
        /// <summary>
        /// Tell this control to respond to the global hotkeys
        /// </summary>
        public void AttachToGlobalHotkeys()
        { 
            GlobalCommands.IncrementSectionNumber.RegisterCommand(VirtualizingGrid.IncrementCommand);
            GlobalCommands.DecrementSectionNumber.RegisterCommand(VirtualizingGrid.DecrementCommand); 
        }
         */
    }
}
