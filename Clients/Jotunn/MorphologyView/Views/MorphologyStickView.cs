using AnnotationVizLib;
using MonogameWPFLibrary.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace MorphologyView.Views
{
    class MorphologyStickView : DependencyObject
    {
        public MorphologyGraph Graph
        {
            get { return (MorphologyGraph)GetValue(GraphProperty); }
            set { SetValue(GraphProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Graph.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GraphProperty =
            DependencyProperty.Register("Graph", typeof(MorphologyGraph), typeof(MorphologyStickView), new PropertyMetadata(null, OnMorphologyGraphChanged));




        public ObservableCollection<MonogameWPFLibrary.ViewModels.MeshViewModel> Annotations
        {
            get { return (ObservableCollection<MeshViewModel>)GetValue(AnnotationsProperty); }
            set { SetValue(AnnotationsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Annotations.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AnnotationsProperty =
            DependencyProperty.Register("Annotations", typeof(ObservableCollection<MonogameWPFLibrary.ViewModels.MeshViewModel>), typeof(MorphologyStickView), new PropertyMetadata(new ObservableCollection<MeshViewModel>()));
          

        private static void OnMorphologyGraphChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            MorphologyStickView sv = o as MorphologyStickView;  
        }

        private void CreateMeshForGraph()
        {
            if(Graph == null)
            {
                this.Annotations = new ObservableCollection<MeshViewModel>();
            }
             
        }

    }
}
