using System;
using System.Collections.Generic;
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
                typeof(string[]),
                typeof(StringArrayAutoScroller),
                new FrameworkPropertyMetadata(new string[] { },
                    new PropertyChangedCallback(OnTextArrayChanged)));

            StringArrayAutoScroller.TextArrayIndexProperty = DependencyProperty.Register("TextArrayIndex",
                typeof(int),
                typeof(StringArrayAutoScroller),
                new FrameworkPropertyMetadata(0,
                    new PropertyChangedCallback(OnTextArrayIndexChanged)));
        }

        public string[] TextArray
        {
            get { return (string[])GetValue(StringArrayAutoScroller.TextArrayProperty); }
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
          
        private static void OnTextArrayChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {

        }

        private static void OnTextArrayIndexChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
             
        }
    }
}
