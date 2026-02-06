using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WebAnnotation.UI.Forms
{
    /// <summary>
    /// WPF dialog for finding and opening a structure by ID.
    /// </summary>
    public partial class FindStructureNumberForm
    {
        /// <summary>
        /// Called when the user clicks Go. Receives the structure ID and returns true if the structure was found and displayed.
        /// </summary>
        public Func<long, bool> OnFindStructure { get; set; }

        public FindStructureNumberForm()
        {
            InitializeComponent();
        }

        private long GetStructureId()
        {
            var text = StructureTextBox?.Text ?? string.Empty;
            return long.TryParse(text, out var id) ? id : 0;
        }

        private void Go_Button_Click(object sender, RoutedEventArgs e)
        {
            var structureId = GetStructureId();
            if (structureId <= 0)
                return;

            if (OnFindStructure?.Invoke(structureId) == true)
                Close();
        }

        private void Close_Button_Click(object sender, RoutedEventArgs e) => Close();

        private static bool IsNonNumeric(string text)
        {
            var regex = new Regex("[^0-9]");
            return regex.IsMatch(text);
        }

        private void StructureTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = IsNonNumeric(e.Text);

        private void StructureTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable/disable Go button based on valid input
            var id = GetStructureId();
            GoButton.IsEnabled = id > 0;
        }
    }
}
