using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Viking.UI.WPF.Controls
{
    /// <summary>
    /// A slider with an editable number box beside it, used for every slider in the Viewer and Annotation Preferences
    /// dialogs (decision D-SliderEditBox). Dragging the slider sets <see cref="Value"/> at once, so a dialog's live
    /// preview follows the drag. Typing in the box is free; the typed number is committed by Enter or by leaving the
    /// box, clamped to [<see cref="Minimum"/>, <see cref="Maximum"/>], and the box then shows the stored value. Text that
    /// is not a number reverts to the current value. Escape reverts an uncommitted edit and is consumed only then, so
    /// Escape in an unchanged box still reaches the dialog's Cancel button.
    /// Clicking the slider gives it the keyboard focus, and with focus Left/Down and Right/Up move it one step
    /// (<see cref="SmallChange"/>, which defaults to one tick), snapped and clamped as a drag is, and set
    /// <see cref="Value"/> as a drag does.
    /// </summary>
    /// <remarks>
    /// Text rules (format, parse, clamp) live in <see cref="SliderValueText"/>. <see cref="Value"/> itself is never
    /// coerced: a preference outside the range keeps its value and only the slider thumb is held at the end.
    /// </remarks>
    public partial class SliderWithValueBox : UserControl
    {
        /// <summary>Identifies <see cref="Value"/>; binds two-way by default.</summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(SliderWithValueBox),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        /// <summary>Identifies <see cref="Minimum"/>.</summary>
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderWithValueBox),
                new PropertyMetadata(0.0, OnSliderSettingChanged));

        /// <summary>Identifies <see cref="Maximum"/>.</summary>
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderWithValueBox),
                new PropertyMetadata(1.0, OnSliderSettingChanged));

        /// <summary>Identifies <see cref="TickFrequency"/>.</summary>
        public static readonly DependencyProperty TickFrequencyProperty =
            DependencyProperty.Register(nameof(TickFrequency), typeof(double), typeof(SliderWithValueBox),
                new PropertyMetadata(1.0, OnSliderSettingChanged));

        /// <summary>Identifies <see cref="IsSnapToTickEnabled"/>.</summary>
        public static readonly DependencyProperty IsSnapToTickEnabledProperty =
            DependencyProperty.Register(nameof(IsSnapToTickEnabled), typeof(bool), typeof(SliderWithValueBox),
                new PropertyMetadata(false, OnSliderSettingChanged));

        /// <summary>Identifies <see cref="SmallChange"/>.</summary>
        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(SliderWithValueBox),
                new PropertyMetadata(0.1, OnSliderSettingChanged));

        /// <summary>Identifies <see cref="LargeChange"/>.</summary>
        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(SliderWithValueBox),
                new PropertyMetadata(1.0, OnSliderSettingChanged));

        /// <summary>Identifies <see cref="ValueFormat"/>.</summary>
        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(SliderWithValueBox),
                new PropertyMetadata("G", OnValueFormatChanged));

        /// <summary>Identifies <see cref="BoxWidth"/>.</summary>
        public static readonly DependencyProperty BoxWidthProperty =
            DependencyProperty.Register(nameof(BoxWidth), typeof(double), typeof(SliderWithValueBox),
                new PropertyMetadata(50.0, OnBoxWidthChanged));

        /// <summary>
        /// True while code moves the slider (value or range sync), so the slider's own ValueChanged, including a
        /// coercion when the range changes, is not mistaken for the user dragging it.
        /// </summary>
        private bool syncingSlider;

        /// <summary>The text last written to the box from <see cref="Value"/>; a commit with this text is a no-op.</summary>
        private string shownText = string.Empty;

        /// <summary>Creates the control and pushes the default settings to the slider and box.</summary>
        public SliderWithValueBox()
        {
            InitializeComponent();
            PART_ValueBox.Width = BoxWidth;
            ApplySliderSettings();
            ShowValue();
        }

        /// <summary>The bound preference. Set by slider drags immediately and by typed text on commit.</summary>
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>Lowest value the slider reaches and the lower clamp for typed values.</summary>
        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>Highest value the slider reaches and the upper clamp for typed values.</summary>
        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>Slider tick spacing; with <see cref="IsSnapToTickEnabled"/> the slider moves in these steps.</summary>
        public double TickFrequency
        {
            get => (double)GetValue(TickFrequencyProperty);
            set => SetValue(TickFrequencyProperty, value);
        }

        /// <summary>Snaps slider drags to <see cref="TickFrequency"/>. Typed values are never snapped.</summary>
        public bool IsSnapToTickEnabled
        {
            get => (bool)GetValue(IsSnapToTickEnabledProperty);
            set => SetValue(IsSnapToTickEnabledProperty, value);
        }

        /// <summary>
        /// Slider arrow-key step (as <see cref="System.Windows.Controls.Primitives.RangeBase.SmallChange"/>). When not set,
        /// the step is <see cref="TickFrequency"/>, so an arrow moves the slider one tick whatever the range.
        /// </summary>
        public double SmallChange
        {
            get => (double)GetValue(SmallChangeProperty);
            set => SetValue(SmallChangeProperty, value);
        }

        /// <summary>Slider page step (as <see cref="System.Windows.Controls.Primitives.RangeBase.LargeChange"/>).</summary>
        public double LargeChange
        {
            get => (double)GetValue(LargeChangeProperty);
            set => SetValue(LargeChangeProperty, value);
        }

        /// <summary>
        /// .NET numeric format for the box, such as "F2", "F0", "P0" or "0.##". A percent format also reads typed text
        /// as percent (see <see cref="SliderValueText.IsPercentFormat"/>).
        /// </summary>
        public string ValueFormat
        {
            get => (string)GetValue(ValueFormatProperty);
            set => SetValue(ValueFormatProperty, value);
        }

        /// <summary>Width of the number box in device-independent pixels.</summary>
        public double BoxWidth
        {
            get => (double)GetValue(BoxWidthProperty);
            set => SetValue(BoxWidthProperty, value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SliderWithValueBox)d;
            control.SyncSlider();
            control.ShowValue();
        }

        /// <summary>
        /// The slider's arrow-key step: <see cref="SmallChange"/> when it was set (locally, by a binding or a style),
        /// otherwise one <see cref="TickFrequency"/>.
        /// </summary>
        private double EffectiveSmallChange =>
            DependencyPropertyHelper.GetValueSource(this, SmallChangeProperty).BaseValueSource == BaseValueSource.Default
                ? TickFrequency
                : SmallChange;

        private static void OnSliderSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SliderWithValueBox)d).ApplySliderSettings();
        }

        private static void OnValueFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SliderWithValueBox)d).ShowValue();
        }

        private static void OnBoxWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SliderWithValueBox)d;
            if (control.PART_ValueBox is not null)
                control.PART_ValueBox.Width = (double)e.NewValue;
        }

        /// <summary>Copies range and tick settings to the slider, then puts the thumb back on <see cref="Value"/>.</summary>
        private void ApplySliderSettings()
        {
            if (PART_Slider is null)
                return;

            syncingSlider = true;
            try
            {
                PART_Slider.Minimum = Minimum;
                PART_Slider.Maximum = Maximum;
                PART_Slider.TickFrequency = TickFrequency;
                PART_Slider.IsSnapToTickEnabled = IsSnapToTickEnabled;
                PART_Slider.SmallChange = EffectiveSmallChange;
                PART_Slider.LargeChange = LargeChange;
                PART_Slider.Value = Value;
            }
            finally
            {
                syncingSlider = false;
            }
        }

        private void SyncSlider()
        {
            if (PART_Slider is null)
                return;

            syncingSlider = true;
            try
            {
                PART_Slider.Value = Value;
            }
            finally
            {
                syncingSlider = false;
            }
        }

        /// <summary>Writes <see cref="Value"/> to the box, discarding any uncommitted text.</summary>
        private void ShowValue()
        {
            if (PART_ValueBox is null)
                return;

            shownText = SliderValueText.Format(Value, ValueFormat);
            PART_ValueBox.Text = shownText;
        }

        /// <summary>
        /// Gives the slider the keyboard focus when it is clicked (its thumb and track buttons are not focusable), so the
        /// arrow keys then nudge it. Moving focus off the box commits any typed text first. The click itself is not
        /// handled here, so the drag or page step goes on as usual.
        /// </summary>
        private void OnSliderPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!PART_Slider.IsKeyboardFocused)
                PART_Slider.Focus();
        }

        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (syncingSlider)
                return;

            // SetCurrentValue keeps the caller's binding and still updates its source.
            SetCurrentValue(ValueProperty, e.NewValue);
        }

        /// <summary>
        /// Stores the typed text: clamped number, or the current value for text that is not a number, then shows the
        /// stored value so a clamp or revert is visible. Unedited text is left alone so focusing and leaving the box
        /// never rewrites a value the box shows rounded.
        /// </summary>
        private void CommitTypedText()
        {
            string text = PART_ValueBox.Text;
            if (text == shownText)
                return;

            double resolved = SliderValueText.ResolveTypedValue(text, Value, Minimum, Maximum, ValueFormat);
            SetCurrentValue(ValueProperty, resolved);
            ShowValue();
        }

        private void OnValueBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitTypedText();
                PART_ValueBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && PART_ValueBox.Text != shownText)
            {
                ShowValue();
                PART_ValueBox.SelectAll();
                e.Handled = true;
            }
        }

        private void OnValueBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            PART_ValueBox.SelectAll();
        }

        private void OnValueBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitTypedText();
        }
    }
}
