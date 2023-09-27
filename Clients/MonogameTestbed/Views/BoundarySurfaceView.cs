using AnnotationVizLib;
using Geometry;
using Geometry.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNAGraphics;
using Microsoft.Xna.Framework; 
using Viking.AnnotationServiceTypes.Interfaces;
using Geometry.Meshing;

namespace MonogameTestbed
{     
    internal class BoundarySurfaceViewModel
    { 
        public readonly IStructureType Type;
        public readonly GridVector3[] BoundaryMarkers;
        public readonly TriangulationMesh<TriangulationVertex> TriangulationMesh;
        public readonly GridVector3 Center;
        public readonly Mesh3D<Vertex3D> Mesh;

        public BoundarySurfaceViewModel(IStructureType type, GridVector3[] surface_points)
        {
            Type = type;
            Center = surface_points.Centroid();
            BoundaryMarkers = surface_points.Select(sp => sp - Center).ToArray();
            
            //Ensure points are sorted on XY axis for Delaunay
            Array.Sort<GridVector3>(BoundaryMarkers, new GridVector3ComparerXYZ());
            GridVector2[] sorted_2d_points = BoundaryMarkers.Select(p => p.XY()).ToArray();
            TriangulationMesh = DelaunayMeshGenerator2D.TriangulateToMesh(sorted_2d_points);

            Mesh = new Mesh3D<Vertex3D>();
            Mesh.AddVerticies(BoundaryMarkers.Select(m => new Vertex3D(m)).ToArray());
            Mesh.AddFaces(TriangulationMesh.Faces);
            Mesh.RecalculateNormals();
        }

        /// <summary>
        /// Create a boundary surface view for each unique structure type ID in the graph.  Merging all points for all structures with the same type
        /// </summary>
        /// <param name="graph"></param>
        public static List<BoundarySurfaceViewModel> CreateBoundarySurfaces(MorphologyGraph graph)
        {
            var results = new List<BoundarySurfaceViewModel>();
            GridVector3 scalar = new GridVector3(graph.scale.X.Value, graph.scale.Y.Value, graph.scale.Z.Value);
            foreach(var type_group in graph.Subgraphs.GroupBy(s => s.Value.structureType.ID))
            {
                var locations = type_group.SelectMany(type => type.Value.Nodes.Values);
                
                var verticies = locations.Select(l => l.Center).ToArray(); 

                results.Add(new BoundarySurfaceViewModel(type_group.First().Value.structureType, verticies));
            }

            return results;
        }
    }
}
