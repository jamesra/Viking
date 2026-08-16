using AnnotationVizLib;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonogameWPFLibrary;
using MonogameWPFLibrary.ViewModels;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MorphologyView
{
    /// <summary>
    /// Convert a morphology node to a flat MeshViewModel
    /// </summary>
    public class MorphologyNodeToBillboardMeshViewModelsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            MorphologyNode node = value as MorphologyNode;
            if(node == null)
            {
                ICollection<MorphologyNode> nodes = value as ICollection<MorphologyNode>;
                if (nodes == null)
                {
                    throw new ArgumentException("Expected Morphology node or collection of Morphology Nodes, got " + value.ToString());
                }

                return new System.Collections.ObjectModel.ObservableCollection<MeshViewModel>(nodes.Select(n => ConvertMorphologyNodeToBillboardMeshViewModel(n)));
            }

            return ConvertMorphologyNodeToBillboardMeshViewModel(node);
        }

        public static MeshViewModel ConvertMorphologyNodeToBillboardMeshViewModel(MorphologyNode node)
        { 
            if (node.Location.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE)
                return ConvertGeometryToCircle(node);

            MeshViewModel mesh = new MeshViewModel();

            Geometry.Vector2[] points = node.Location.Geometry.ToPoints().Select(p => new Geometry.Vector2(p.X - node.Graph.BoundingBox.Center[0],
                                                                                                 p.Y - node.Graph.BoundingBox.Center[1])).ToArray();
            Geometry.Vector2 centroid = node.Location.Geometry.Centroid();
            centroid = new Geometry.Vector2(centroid.X - node.Graph.BoundingBox.Center[0],
                                       centroid.Y - node.Graph.BoundingBox.Center[1]);

            Geometry.Vector2[] allPoints = new Geometry.Vector2[points.Length + 1];

            points.CopyTo(allPoints, 0);
            allPoints[allPoints.Length - 1] = centroid;

            //Create verticies for each point 
            mesh.Vertices = allPoints.Select(p => new VertexPositionColor(p.ToXNAVector3(node.Z), Color.Red)).ToArray();
            
            mesh.Faces = CreateEdgesForPointsAroundCenterVertex(points.Length);

            return mesh;
        }

        private static MeshViewModel ConvertGeometryToCircle(MorphologyNode node)
        {
            const int NumPointsOnCircle = 18;
            MeshViewModel mesh = new MeshViewModel();

            Geometry.Vector3[] points = new Geometry.Vector3[NumPointsOnCircle + 1];
            double Radius = Math.Max(node.BoundingBox.dimensions[0], node.BoundingBox.dimensions[1]);

            Geometry.Vector3 translationVector = node.Center - node.Graph.BoundingBox.CenterPoint;

            for (int i = 0; i < NumPointsOnCircle; i++)
            {
                double angle = ((double)i / (double)NumPointsOnCircle) * Math.PI * 2.0;
                points[i] = new Geometry.Vector3(Math.Cos(angle) * Radius, Math.Sin(angle) * Radius, node.Z);
                points[i] += translationVector;
            } 

            points[NumPointsOnCircle] = new Geometry.Vector3(0, 0, node.Z);
            points[NumPointsOnCircle] += translationVector;

            mesh.Vertices = points.Select(p => new VertexPositionColor(p.ToXNAVector3(), Color.Blue)).ToArray();

            mesh.Faces = CreateEdgesForPointsAroundCenterVertex(NumPointsOnCircle);

            return mesh;
        }

        /// <summary>
        /// Create an integer array of triangle edges for points laid sequentially around the border of a shape, with the last index of the points being the center point
        /// </summary>
        /// <param name="numVerts"></param>
        /// <returns></returns>
        private static int[] CreateEdgesForPointsAroundCenterVertex(int numVertsAroundEdge)
        {
            int[] edges = new int[numVertsAroundEdge * 3];
            int iEdge = 0;
            int iCentroid = numVertsAroundEdge;
            //Determine the edges
            for (int iVert = 0; iVert < numVertsAroundEdge - 1; iVert++)
            {
                edges[iEdge++] = iVert;
                edges[iEdge++] = iVert + 1;
                edges[iEdge++] = iCentroid;
            }

            edges[iEdge++] = iCentroid - 1;
            edges[iEdge++] = 0;
            edges[iEdge++] = iCentroid;

            if (iEdge != edges.Length)
            {
                throw new ArgumentException("Length of edges array incorrect for number of edges generated");
            }

            return edges;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
