using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MorphologyView.ViewModels
{
    class MorphologyGraphViewModel : DependencyObject
    {
        public AnnotationVizLib.MorphologyGraph Graph
        {
            get { return (AnnotationVizLib.MorphologyGraph)GetValue(GraphProperty); }
            set { SetValue(GraphProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Graph.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GraphProperty =
            DependencyProperty.Register("Graph", typeof(AnnotationVizLib.MorphologyGraph), typeof(MorphologyGraphViewModel), new PropertyMetadata());
    }
}
