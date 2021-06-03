using System;
using System.Globalization;
using System.Windows.Data;

namespace MorphologyView
{
    class DynamicRenderMeshToMeshViewModelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
            /*
            DynamicRenderMesh mesh = value as DynamicRenderMesh;
            if(mesh == null)
            {
                throw new ArgumentException("Argument must be a DynamicRenderMesh");
            }

            MeshViewModel model = new MeshViewModel();
            model.Verticies = mesh.Verticies.Select(v => new VertexPositionColor(v.Position.ToXNAVector3(), Color.Blue)).ToArray();
            model.Faces = mesh.Faces.SelectMany(f => f.iVerts).ToArray();
            return model;
            */
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
