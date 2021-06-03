using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using WebAnnotationModel;

namespace WebAnnotation.UI.Forms
{
    /// <summary>
    /// Interaction logic for FindStructureForm.xaml
    /// </summary>
    public partial class GoToStructureForm
    {

        public Int64 ID;

        /// <summary>
        /// Called when the user requests we go to an ID
        /// </summary>
        public event Action<Int64> OnGo;

        public GoToStructureForm()
        {
            InitializeComponent();
        }
         
        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.ID = System.Convert.ToInt64(this.NumberTextbox.Text);
            }
            catch (FormatException)
            {
                return;
            }

            if (Store.Locations.GetObjectByID(this.ID, true) != null)
            {
                if (OnGo != null)
                    this.Dispatcher.BeginInvoke(new Action(() => { OnGo(this.ID); } ) );

                //Todo: Fire an event or set a property the creator can subscribe to, or pass a delegate
                //WebAnnotation.AnnotationOverlay.GoToStructure(this.ID);
                this.Close();
            }
        }

        private void Go_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.ID = System.Convert.ToInt64(this.NumberTextbox.Text);
            }
            catch (FormatException)
            {
                return;
            }

            if (OnGo != null)
                this.Dispatcher.BeginInvoke(new Action(() => { OnGo(this.ID); }));

            //WebAnnotation.AnnotationOverlay.GoToStructure(this.ID);
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private static bool IsTextAllowed(string text)
        {
            Regex regex = new Regex("[^0-9]*"); //regex that matches disallowed text
            return regex.IsMatch(text);
        }

        private void NumberTextbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }
    }
}
