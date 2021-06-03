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
            if (node.Location.TypeCode == Annotation.Interfaces.LocationType.CIRCLE)
                return ConvertGeometryToCircle(node);

            MeshViewModel mesh = new MeshViewModel();

            GridVector2[] points = node.Location.Geometry.ToPoints().Select(p => new GridVector2(p.X - node.Graph.BoundingBox.Center[0],
                                                                                                 p.Y - node.Graph.BoundingBox.Center[1])).ToArray();
            GridVector2 centroid = node.Location.Geometry.Centroid();
            centroid = new GridVector2(centroid.X - node.Graph.BoundingBox.Center[0],
                                       centroid.Y - node.Graph.BoundingBox.Center[1]);

            GridVector2[] allPoints = new GridVector2[points.Length + 1];

            points.CopyTo(allPoints, 0);
            allPoints[allPoints.Length - 1] = centroid;

            //Create verticies for each point 
            mesh.Verticies = allPoints.Select(p => new VertexPositionColor(p.ToXNAVector3(node.Z), Color.Red)).ToArray();
            
            mesh.Faces = CreateEdgesForPointsAroundCenterVertex(points.Length);

            return mesh;
        }

        private static MeshViewModel ConvertGeometryToCircle(MorphologyNode node)
        {
            const int NumPointsOnCircle = 18;
            MeshViewModel mesh = new MeshViewModel();

            GridVector3[] points = new GridVector3[NumPointsOnCircle + 1];
            double Radius = Math.Max(node.BoundingBox.dimensions[0], node.BoundingBox.dimensions[1]);

            GridVector3 translationVector = node.Center - node.Graph.BoundingBox.CenterPoint;

            for (int i = 0; i < NumPointsOnCircle; i++)
            {
                double angle = ((double)i / (double)NumPointsOnCircle) * Math.PI * 2.0;
                points[i] = new GridVector3(Math.Cos(angle) * Radius, Math.Sin(angle) * Radius, node.Z);
                points[i] += translationVector;
            } 

            points[NumPointsOnCircle] = new GridVector3(0, 0, node.Z);
            points[NumPointsOnCircle] += translationVector;

            mesh.Verticies = points.Select(p => new VertexPositionColor(p.ToXNAVector3(), Color.Blue)).ToArray();

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
