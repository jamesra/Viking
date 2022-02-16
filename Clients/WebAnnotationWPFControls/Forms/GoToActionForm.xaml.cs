using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using log4net.Util;
using WebAnnotationModel;

namespace WebAnnotation.UI.Forms
{
    /// <summary>
    /// Interaction logic for GoToActionForm.xaml
    /// </summary>
    public partial class GoToActionForm
    {
        public static readonly DependencyProperty InputProperty = DependencyProperty.Register(
            "Input", typeof(string), typeof(GoToActionForm), new PropertyMetadata(default(string)));

        public string Input
        {
            get => (string)GetValue(InputProperty);
            set
            {
                SetValue(InputProperty, value);
                try
                {
                    this.ID = System.Convert.ToInt64(this.NumberTextbox.Text);
                }
                catch (FormatException)
                {
                    ID = default;
                    return;
                }
            }
        }

        public static readonly DependencyProperty IDProperty = DependencyProperty.Register(
            "ID", typeof(long), typeof(GoToActionForm), new PropertyMetadata(default));

        public long ID
        {
            get => (long)GetValue(IDProperty);
            set
            {
                SetValue(IDProperty, value);
            }
        }

        public static readonly DependencyProperty IsActionEnabledProperty = DependencyProperty.Register(
            "IsActionEnabled", typeof(bool), typeof(GoToActionForm), new PropertyMetadata(false));

        public bool IsActionEnabled
        {
            get => (bool)GetValue(IsActionEnabledProperty);
            set => SetValue(IsActionEnabledProperty, value);
        }



        /// <summary>
        /// Returns true if the current ID is valid
        /// </summary>
        public Func<long, bool> IsValidInput;

        /// <summary>
        /// Called when the user requests we go to an ID. 
        /// </summary>
        public Action<long> OnGo;

        public GoToActionForm()
        {
            DataContext = this;
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

            if (IsValidInput(ID)) //Store.Locations.GetObjectByID(this.ID, true) != null)
            {
                OnGo?.Invoke(ID);

                //TODO: Set a property that fires an event so WebAnnotation can travel where it needs to go
                //WebAnnotation.AnnotationOverlay.GoToLocation(this.ID);
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

            OnGo?.Invoke(ID);

            //WebAnnotation.AnnotationOverlay.GoToLocation(this.ID);
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private static bool IsNonNumeric(string text)
        {
            Regex regex = new Regex("[^0-9]"); //regex that matches disallowed text
            return regex.IsMatch(text);
        }

        private void NumberTextbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = IsNonNumeric(e.Text);
        }

        private CancellationTokenSource cancelIDUpdateTokenSource;

        private void NumberTextbox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                this.ID = System.Convert.ToInt64(this.NumberTextbox.Text);
            }
            catch (FormatException)
            {
                ID = default;
                IsActionEnabled = false;
                return;
            }

            QueueEnabledCheck(ID);
        }

        private void QueueEnabledCheck(long ID)
        {
            //Cancel any existing enabled checks if they exist
            var newCancelTokenSource = new CancellationTokenSource();
            var originalCancellationToken = Interlocked.Exchange(ref cancelIDUpdateTokenSource, newCancelTokenSource);
            originalCancellationToken?.Cancel();
            
            Task.Run(() => EnabledCheckTask(ID, newCancelTokenSource.Token), newCancelTokenSource.Token);
        }

        private Task EnabledCheckTask(long ID, CancellationToken token)
        {
            try
            {
                var result = IsValidInput(ID);
                if (token.IsCancellationRequested)
                    return Task.FromCanceled(token);

                this.Dispatcher.BeginInvoke(new Action(()=>IsActionEnabled = result));
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                return Task.FromCanceled(token);
            }

            return Task.CompletedTask;
        }
    }
}
