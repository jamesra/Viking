using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Viking.WPF
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class StringArrayAutoScroller : UserControl
    {
         
        public static readonly DependencyProperty TextArrayProperty;
        public static readonly DependencyProperty TextArrayIndexProperty;

        static StringArrayAutoScroller()
        {
            StringArrayAutoScroller.TextArrayProperty = DependencyProperty.Register("TextArray",
                typeof(ObservableCollection<string>),
                typeof(StringArrayAutoScroller),
                new FrameworkPropertyMetadata(new ObservableCollection<string>(new string[] { "Default" }),
                    new PropertyChangedCallback(OnTextArrayChanged)));

            StringArrayAutoScroller.TextArrayIndexProperty = DependencyProperty.Register("TextArrayIndex",
                typeof(int),
                typeof(StringArrayAutoScroller),
                new FrameworkPropertyMetadata(0,
                    new PropertyChangedCallback(OnTextArrayIndexChanged)));
        }

        public bool IsDropDownOpen
        {
            get { return comboHelpText.IsDropDownOpen; }
            set { comboHelpText.IsDropDownOpen = value; }
        }

        public ObservableCollection<string> TextArray
        {
            get { return (ObservableCollection<string>)GetValue(StringArrayAutoScroller.TextArrayProperty); }
            set { SetValue(StringArrayAutoScroller.TextArrayProperty, value); }
        }

        public int TextArrayIndex
        {
            get { return (int)GetValue(StringArrayAutoScroller.TextArrayIndexProperty); }
            set { SetValue(StringArrayAutoScroller.TextArrayIndexProperty, value); }
        }

        public StringArrayAutoScroller()
        {
            InitializeComponent();
        }

        private void OnCollectionChanged(object o, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                return;

            int TotalCount = e.NewItems.Count + e.NewStartingIndex;

            if (TotalCount > 0)
            {
                Random R = new Random();
                this.TextArrayIndex = R.Next() % TotalCount;
                this.comboHelpText.SelectedIndex = this.TextArrayIndex;
            } 
        }
          
        private static void OnTextArrayChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            StringArrayAutoScroller obj = o as StringArrayAutoScroller;
            if (obj != null)
            {
                ObservableCollection<string> oldCollection = e.OldValue as ObservableCollection<string>;
                if (oldCollection != null)
                    oldCollection.CollectionChanged -= obj.OnCollectionChanged;

                ObservableCollection<string> newCollection = e.NewValue as ObservableCollection<string>;
                if (newCollection != null)
                    newCollection.CollectionChanged += obj.OnCollectionChanged;
            }
        }

        private static void OnTextArrayIndexChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            StringArrayAutoScroller obj = o as StringArrayAutoScroller;
            if(obj != null)
            {
                obj.SetComboText((int)e.NewValue);
            }
        }

        private void SetComboText(int index)
        {
            if (this.TextArray != null && this.TextArray.Count > 0)
                this.comboHelpText.Text = this.TextArray[index % this.TextArray.Count];
        }
    }
}
