using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using AnnotationVizLib;
using MonogameWPFLibrary;
using SqlGeometryUtils;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonogameWPFLibrary.ViewModels;
using System;
using Geometry.Meshing;

namespace MorphologyView
{
    class DynamicRenderMeshToMeshViewModelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DynamicRenderMesh mesh = value as DynamicRenderMesh;
            if(mesh == null)
            {
                throw new ArgumentException("Argument must be a DynamicRenderMesh");
            }

            MeshViewModel model = new MeshViewModel();
            model.Verticies = mesh.Verticies.Select(v => new VertexPositionColor(v.Position.ToXNAVector3(), Color.Blue)).ToArray();
            model.Faces = mesh.Faces.SelectMany(f => f.iVerts).ToArray();
            return model;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
