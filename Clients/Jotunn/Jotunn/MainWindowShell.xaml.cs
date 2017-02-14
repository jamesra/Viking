using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Prism;
using Prism.Regions;
using Prism.Modularity;
using Jotunn.Common;

namespace Jotunn
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, IShellView
	{
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
        

		public MainWindow()
		{
            SOTC_BindingErrorTracer.BindingErrorTraceListener.SetTrace();
			this.InitializeComponent();
            /*
			// Insert code required on object creation below this point.
            KeyBinding ib = new KeyBinding(
                GlobalCommands.IncrementSectionNumber, new KeyGesture(Key.U, ModifierKeys.Control));
            this.InputBindings.Add(ib);
            */
            /*
             incrementCenterIndexCommand = new RoutedUICommand("Increments the section number", "IncrementCenterIndexCommand", typeof(MainWindow));
             decrementCenterIndexCommand = new RoutedUICommand("Decrements the section number", "DecrementCenterIndexCommand", typeof(MainWindow));

             CommandBinding cb = new CommandBinding(incrementCenterIndexCommand, OnIncrementSectionNumber);
             this.CommandBindings.Add(cb);

             cb = new CommandBinding(decrementCenterIndexCommand, OnDecrementSectionNumber);
             this.CommandBindings.Add(cb);
             */

            Prism.Commands.DelegateCommand incrementCommand = new Prism.Commands.DelegateCommand(IncrementSectionNumber);
            Prism.Commands.DelegateCommand decrementCommand = new Prism.Commands.DelegateCommand(IncrementSectionNumber);

            GlobalCommands.IncrementSectionNumber.RegisterCommand(incrementCommand);
            GlobalCommands.DecrementSectionNumber.RegisterCommand(decrementCommand);
            //GlobalCommands.IncrementSectionNumber.Execute(null);
                 
            OnStartup(null);
		}

        /// <summary>
        /// This registers a class handlers for key-presses so we can use global hotkeys that work from anywhere in the application
        /// </summary>
        /// <param name="e"></param>
        protected void OnStartup(StartupEventArgs e)
        {
            EventManager.RegisterClassHandler(typeof(Window), System.Windows.UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDownPreview));
        }

        protected void OnKeyDownPreview(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Insert)
            {
                GlobalCommands.IncrementSectionNumber.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                GlobalCommands.DecrementSectionNumber.Execute(null);
                e.Handled = true;
            }

            e.Handled = false;
        }

        #region IShellView Members

        private void OnIncrementSectionNumber(object sender, ExecutedRoutedEventArgs e)
        {
            IncrementSectionNumber();
        }

        private void IncrementSectionNumber()
        {
            Trace.WriteLine("OnIncrementSectionNumber keys do sometimes work");
        }

        private void OnDecrementSectionNumber(object sender, ExecutedRoutedEventArgs e)
        {
            DecrementSectionNumber();
        }

        private void DecrementSectionNumber()
        {
            Trace.WriteLine("OnDecrementSectionNumber keys do sometimes work");
        }

        void IShellView.ShowView()
        {
            this.Show(); 
        }

        #endregion

        private void Window_Closed(object sender, EventArgs e)
        {
            this.Close(); 
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