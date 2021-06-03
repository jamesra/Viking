using Geometry;
using Geometry.Meshing;
using System;
using System.Linq;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Meshing;


namespace MorphologyMesh
{

    public static class ShapeMeshGenerator<VERTEX, DATA>
        where VERTEX : IVertex3D<DATA>
        
    {
        public static Mesh3D<IVertex3D<DATA>> CreateMeshForBox(GridBox box, DATA locationData, GridVector3 translate)
        {
            Mesh3D<IVertex3D<DATA>> mesh = new Mesh3D<IVertex3D<DATA>>();

            double[] minVals = box.minVals;
            double[] maxVals = box.maxVals;

            double minX = minVals[0];
            double minY = minVals[1];
            double minZ = minVals[2];

            double maxX = maxVals[0];
            double maxY = maxVals[1];
            double maxZ = maxVals[2];

            IVertex3D<DATA>[] verts = new IVertex3D<DATA>[] { new Vertex3D<DATA>(new GridVector3(minX, minY, minZ), GridVector3.Zero),
                                            new Vertex3D<DATA>(new GridVector3(maxX, minY, minZ), GridVector3.Zero),
                                            new Vertex3D<DATA>(new GridVector3(minX, minY, maxZ), GridVector3.Zero),
                                            new Vertex3D<DATA>(new GridVector3(maxX, minY, maxZ), GridVector3.Zero),
                                            new Vertex3D<DATA>(new GridVector3(minX, maxY, minZ), GridVector3.Zero),
                                            new Vertex3D<DATA>(new GridVector3(maxX, maxY, minZ), GridVector3.Zero),
                                            new Vertex3D<DATA>(new GridVector3(minX, maxY, maxZ), GridVector3.Zero),
                                            new Vertex3D<DATA>(new GridVector3(maxX, maxY, maxZ), GridVector3.Zero)};

            foreach (var v in verts)
            {
                v.Normal = v.Position - box.CenterPoint;
                v.Normal.Normalize();
            }

            mesh.AddVerticies(verts);

            Face[] faces = new Face[] {
                         new Face(1,3,2,0), //Bottom
                         new Face(4,5,1,0), //Front
                         new Face(4,6,7,5), //Top
                         new Face(3,7,6,2), //Back
                         new Face(5,7,3,1), //Right
                         new Face(2,6,4,0) }; //Left

            mesh.AddFaces(faces.Select(f => f as IFace).ToArray());

            return mesh;
        }

        public static Mesh3D<IVertex3D<DATA>> CreateMeshForCircle(ICircle2D circle, double Z, int NumPointsOnCircle, DATA locationData, GridVector3 translate)
        {
            Mesh3D<IVertex3D<DATA>> mesh = new Mesh3D<IVertex3D<DATA>>();
            mesh.AddVerticies(CreateVerticiesForCircle(circle, Z, TopologyMeshGenerator.NumPointsAroundCircle, locationData, translate));
            AddFacesToCircle(mesh, TopologyMeshGenerator.NumPointsAroundCircle);

            return mesh;
        }

        public static Mesh3D<IVertex3D<DATA>> CreateMeshForDisc(ICircle2D circle, double Z, double Height, int NumPointsOnDisc, DATA locationData, GridVector3 translate)
        {
            Mesh3D<IVertex3D<DATA>> mesh = new Mesh3D<IVertex3D<DATA>>();
            double halfHeight = Height / 2.0;
            mesh.AddVerticies(CreateVerticiesForCircle(circle, Z - halfHeight, TopologyMeshGenerator.NumPointsAroundCircle, locationData, translate));
            AddFacesToCircle(mesh, TopologyMeshGenerator.NumPointsAroundCircle, 0, CCWNormalHasPositiveZ: false);
            mesh.AddVerticies(CreateVerticiesForCircle(circle, Z + halfHeight, TopologyMeshGenerator.NumPointsAroundCircle, locationData, translate));
            AddFacesToCircle(mesh, TopologyMeshGenerator.NumPointsAroundCircle, NumPointsOnDisc + 1, CCWNormalHasPositiveZ: true);

            AddFacesToDiscRim(mesh, TopologyMeshGenerator.NumPointsAroundCircle);

            return mesh;
        }

        public static IVertex3D<DATA>[] CreateVerticiesForCircle(ICircle2D circle, double Z, int NumPointsOnCircle, DATA locationID, GridVector3 translate)
        {
            IVertex3D<DATA>[] verts = new IVertex3D<DATA>[NumPointsOnCircle + 1];
            GridVector3 translationVector = new GridVector3(circle.Center.X, circle.Center.Y, Z) + translate;

            for (int i = 0; i < NumPointsOnCircle; i++)
            {
                double angle = ((double)i / (double)NumPointsOnCircle) * Math.PI * 2.0;
                GridVector3 position = new GridVector3(Math.Cos(angle) * circle.Radius, Math.Sin(angle) * circle.Radius, 0);
                verts[i] = new Vertex3D<DATA>(position,
                                             new GridVector3(Math.Sin(angle), Math.Cos(angle), 0),
                                             locationID);
                verts[i].Position += translationVector;
            }

            verts[NumPointsOnCircle] = new Vertex3D<DATA>(new GridVector3(0, 0, 0),
                                                         new GridVector3(0, 0, Z > 0 ? 1 : -1),
                                                         locationID);
            verts[NumPointsOnCircle].Position += translationVector;

            return verts;
        }

        private static void AddFacesToCircle(Mesh3D<IVertex3D<DATA>> mesh, int NumPointsOnCircle, int firstCircleVertex = 0, bool CCWNormalHasPositiveZ = true)
        {
            int[] edges = new int[NumPointsOnCircle * 3];
            int iCentroid = firstCircleVertex + NumPointsOnCircle;
            //Determine the edges
            for (int iVert = firstCircleVertex; iVert < iCentroid - 1; iVert++)
            {
                if(CCWNormalHasPositiveZ)
                    mesh.AddFace(iVert, iVert + 1, iCentroid);
                else
                    mesh.AddFace(iVert+1, iVert, iCentroid);
            }

            if(CCWNormalHasPositiveZ)
                mesh.AddFace( iCentroid - 1, firstCircleVertex, iCentroid);
            else
                mesh.AddFace(firstCircleVertex, iCentroid - 1, iCentroid);
        }

        /// <summary>
        /// Add quadrilaterals around the edge of a disc.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="NumPointsOnCircle"></param>
        /// <param name="firstCircleVertex"></param>
        private static void AddFacesToDiscRim(Mesh3D<IVertex3D<DATA>> mesh, int NumPointsOnCircle, int firstCircleVertex = 0)
        {
            int iFirstLowerVertex = firstCircleVertex;
            int iLowerCentroid = iFirstLowerVertex + NumPointsOnCircle;
            int iFirstUpperVertex = iLowerCentroid + 1;
            int iUpperCentroid = iFirstUpperVertex + NumPointsOnCircle;

            //Determine the edges

            for (int i = 0; i < NumPointsOnCircle - 1; i++)
            {
                int iLowerVert = i + iFirstLowerVertex;
                int iUpperVert = i + iFirstUpperVertex;
                mesh.AddFace(new Face(iLowerVert, iLowerVert + 1, iUpperVert + 1, iUpperVert));
            }

            mesh.AddFace(new Face(iFirstLowerVertex + NumPointsOnCircle - 1, iFirstLowerVertex, iFirstUpperVertex, iFirstUpperVertex + NumPointsOnCircle - 1));
        }

        public static Mesh3D<IVertex3D<DATA>> CreateMeshForPolygon(IPolygon2D polygon, double Z, DATA locationData, GridVector3 translate)
        {
            IMesh triangulation = polygon.Triangulate();
            Mesh3D<IVertex3D<DATA>> mesh = ToDynamicRenderMesh(triangulation, Z, locationData, translate);
            return mesh;
        }

        public static Mesh3D<IVertex3D<DATA>> CreateMeshForPolygonSlab(IPolygon2D polygon, double Z, double Height, DATA locationData, GridVector3 translate)
        {
            IMesh triangulation = polygon.Triangulate();
            Mesh3D<IVertex3D<DATA>> mesh = new Mesh3D<IVertex3D<DATA>>();
            double HalfHeight = Height / 2.0;

            IVertex3D<DATA>[] bottom_verticies = CreateVerticiesForPolygon(triangulation, Z - HalfHeight, locationData, translate);
            IVertex3D<DATA>[] top_verticies = CreateVerticiesForPolygon(triangulation, Z + HalfHeight, locationData, translate);
            
            mesh.AddVerticies(bottom_verticies);
            mesh.AddVerticies(top_verticies);

            AddFacesToPolygon(mesh, triangulation, 0, false);
            AddFacesToPolygon(mesh, triangulation, bottom_verticies.Length, true);

            AddFacesToPolygonRim(mesh, triangulation, polygon.Convert());

            mesh.RecalculateNormals();
            return mesh;
        }

        public static IVertex3D<DATA>[] CreateVerticiesForPolygon(IMesh triangulation, double Z, DATA locationID, GridVector3 translate)
        {
            IVertex3D<DATA>[] verticies = new Vertex3D<DATA>[triangulation.Vertices.Count];
            
            int iVert = 0;  
            foreach (TriangleNet.Geometry.Vertex v in triangulation.Vertices)
            {
                Geometry.Meshing.IVertex3D<DATA> vertex = new Geometry.Meshing.Vertex3D<DATA>(new GridVector3(v.X, v.Y, Z) + translate,
                                                                              new GridVector3(0,0,1), locationID);
                verticies[v.ID] = vertex;
                iVert++;
            }

            return verticies;
        }

        public static IVertex3D<DATA>[] CreateVerticiesForPolygon(IPolygon2D polygon, double Z, DATA locationID, GridVector3 translate)
        {
            Vertex3D<DATA>[] verticies = new Vertex3D<DATA>[polygon.TotalUniqueVerticies];

            int iVert = 0;
            for(int iExterior = 0; iExterior < polygon.ExteriorRing.Count - 1; iExterior++)
            {
                IPoint2D v = polygon.ExteriorRing.ElementAt(iExterior);
                Geometry.Meshing.Vertex3D<DATA> vertex = new Geometry.Meshing.Vertex3D<DATA>(new GridVector3(v.X, v.Y, Z) + translate,
                                                                                   new GridVector3(0, 0, 1), locationID);
                verticies[iVert] = vertex;
                iVert++;
            }

            foreach(var innerPolygon in polygon.InteriorRings)
            {
                for (int iExterior = 0; iExterior < innerPolygon.Length - 1; iExterior++)
                {
                    IPoint2D v = innerPolygon.ElementAt(iExterior);
                    Geometry.Meshing.Vertex3D<DATA> vertex = new Geometry.Meshing.Vertex3D<DATA>(new GridVector3(v.X, v.Y, Z) + translate,
                                                                                       new GridVector3(0, 0, 1), locationID);
                    verticies[iVert] = vertex;
                    iVert++;
                }
            } 

            return verticies;
        }

        private static void AddFacesToPolygon(IMesh3D<IVertex3D<DATA>> drmesh, IMesh triangulation, int firstPolyVertex = 0, bool CCWNormalHasPositiveZ = true)
        {
            foreach (var t in triangulation.Triangles)
            {
                Face f;
                if(CCWNormalHasPositiveZ)
                    f = new Face(t.GetVertexID(0) + firstPolyVertex, t.GetVertexID(1) + firstPolyVertex, t.GetVertexID(2) + firstPolyVertex);
                else
                    f = new Face(t.GetVertexID(2) + firstPolyVertex, t.GetVertexID(1) + firstPolyVertex, t.GetVertexID(0) + firstPolyVertex);

                drmesh.AddFace(f);
            } 
        }

        /// <summary>
        /// Add quadrilaterals around the edge of a polygon slab.  Indicies in mesh must match indicies in triangulation
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="NumPointsOnCircle"></param>
        /// <param name="firstCircleVertex"></param>
        private static void AddFacesToPolygonRim(IMesh3D<IVertex3D<DATA>> mesh, IMesh triangulation, GridPolygon polygon)
        { 
            int VertsInTriangulation = triangulation.Vertices.Count;

            //For each group of labels, locate the exterior ring and internal rings by using the vertex edge labels

            AddFacesToPolygonRim(mesh,
                                 polygon.ExteriorRing.Length - 1, //Subtrace 1 because the last point is a duplicate in GridPolygon's data structure
                                 0, triangulation.Vertices.Count, true);

            int iFirstVertex = polygon.ExteriorRing.Length - 1;//Subtrace 1 because the last point is a duplicate in GridPolygon's data structure

            foreach (GridPolygon innerPoly in polygon.InteriorPolygons)
            {
                AddFacesToPolygonRim(mesh, innerPoly.ExteriorRing.Length - 1, //Subtrace 1 because the last point is a duplicate in GridPolygon's data structure
                                     iFirstVertex, 
                                     iFirstVertex + triangulation.Vertices.Count, 
                                     FaceOutside: false);
                iFirstVertex += innerPoly.ExteriorRing.Length - 1; //Subtrace 1 because the last point is a duplicate in GridPolygon's data structure
            }
        }

        /// <summary>
        /// Add quadrilaterals around the edge of a disc.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="NumPointsOnCircle"></param>
        /// <param name="lowerFaceFirstVertex"></param>
        private static void AddFacesToPolygonRim(IMesh3D<IVertex3D<DATA>> mesh, int NumPointsInRim, int lowerFaceFirstVertex, int upperFaceFirstVertex, bool FaceOutside)
        {
            int iFirstLowerVertex = lowerFaceFirstVertex;
            int iFirstUpperVertex = upperFaceFirstVertex;

            //Determine the edges

            for (int i = 0; i < NumPointsInRim - 1; i++)
            {
                int iLowerVert = i + iFirstLowerVertex;
                int iUpperVert = i + iFirstUpperVertex;
                if(FaceOutside)
                    mesh.AddFace(new Face(iLowerVert, iLowerVert + 1, iUpperVert + 1, iUpperVert));
                else
                    mesh.AddFace(new Face(iUpperVert, iUpperVert + 1, iLowerVert + 1, iLowerVert));
            }

            if(FaceOutside)
                mesh.AddFace(new Face(iFirstLowerVertex + NumPointsInRim - 1, iFirstLowerVertex, iFirstUpperVertex, iFirstUpperVertex + NumPointsInRim - 1));
            else
                mesh.AddFace(new Face(iFirstUpperVertex + NumPointsInRim - 1, iFirstUpperVertex, iFirstLowerVertex, iFirstLowerVertex + NumPointsInRim - 1));
        }

        private static Mesh3D<IVertex3D<DATA>> ToDynamicRenderMesh(TriangleNet.Meshing.IMesh triangulation, double Z, DATA locationData, GridVector3 translate)
        {
            Mesh3D<IVertex3D<DATA>> drmesh = new Mesh3D<IVertex3D<DATA>>();

            foreach (TriangleNet.Geometry.Vertex v in triangulation.Vertices)
            {
                Geometry.Meshing.Vertex3D<DATA> vertex = new Geometry.Meshing.Vertex3D<DATA>(new GridVector3(v.X, v.Y, Z) + translate,
                                                                              GridVector3.Zero, locationData);
                drmesh.AddVertex(vertex);
            }

            foreach (var t in triangulation.Triangles)
            {
                Face f = new Face(t.GetVertexID(0), t.GetVertexID(1), t.GetVertexID(2));
                drmesh.AddFace(f);
            }

            return drmesh;
        }

        public static Vertex3D<DATA>[] CreateVerticiesForPolyline(IPolyLine2D polyline, double Z, DATA locationID, GridVector3 translate)
        {
            Vertex3D<DATA>[] verticies = new Vertex3D<DATA>[polyline.Points.Count];

            int iVert = 0;
            foreach (IPoint2D p in polyline.Points)
            {
                Geometry.Meshing.Vertex3D<DATA> vertex = new Geometry.Meshing.Vertex3D<DATA>(new GridVector3(p.X, p.Y, Z) + translate,
                                                                              new GridVector3(0, 0, 1), locationID);
                verticies[iVert] = vertex;
                iVert++;
            }

            return verticies;
        }

        public static Vertex3D<DATA>[] CreateVerticiesForPoint(IPoint2D p, double Z, DATA locationID, GridVector3 translate)
        {
            Vertex3D<DATA>[] verticies = new Vertex3D<DATA>[1];

            verticies[0] = new Geometry.Meshing.Vertex3D<DATA>(new GridVector3(p.X, p.Y, Z) + translate,
                                                              new GridVector3(0, 0, 1), locationID);

            return verticies;
        }
    }
}
