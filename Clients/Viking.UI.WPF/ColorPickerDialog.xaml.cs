using System.Windows;
using System.Windows.Media;

namespace Viking.UI.WPF
{
    public partial class ColorPickerDialog : Window
    {
        public Color SelectedColor { get; private set; }

        public ColorPickerDialog(Color initial)
        {
            InitializeComponent();
            RedSlider.Value = initial.R;
            GreenSlider.Value = initial.G;
            BlueSlider.Value = initial.B;
            UpdatePreview();
        }

        private void OnColorChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
                return;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            SelectedColor = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
            Preview.Background = new SolidColorBrush(SelectedColor);
            RedValue.Text = ((int)RedSlider.Value).ToString();
            GreenValue.Text = ((int)GreenSlider.Value).ToString();
            BlueValue.Text = ((int)BlueSlider.Value).ToString();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
