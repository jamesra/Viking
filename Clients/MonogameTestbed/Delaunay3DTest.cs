using Geometry;
using Geometry.Meshing;
using MIConvexHull;
using MIConvexHullExtensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    class DelaunayTetrahedronView
    {
        public Polygon[] Polygons = null;
        public double[] PolyZ = null;

        public List<Mesh3D> meshes = [];

        public DelaunayTetrahedronView(Polygon[] polys, double[] Z)
        {
            Polygons = polys;
            PolyZ = Z;

            meshes = UpdateTriangulation3D();
        }

        public List<Mesh3D> UpdateTriangulation3D()
        {
            List<MIVector3> listPoints = [];
            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)
            {
                var map = Polygons[iPoly].CreatePointToPolyMap();
                double Z = PolyZ[iPoly];
                listPoints.AddRange(map.Keys.Select((Func<Geometry.Vector2, MIVector3>)(k => new MIVector3((Geometry.Vector3)k.ToVector3(Z), (PolygonIndex)new PolygonIndex((int)iPoly, (int?)map[(Geometry.Vector2)k].InnerShapeIndex, (int)map[(Geometry.Vector2)k].VertexIndex, (IReadOnlyList<Polygon>)Polygons)))));
            }

            DelaunayTriangulation<MIVector3, DefaultTriangulationCell<MIVector3>> tri = MIConvexHull.DelaunayTriangulation<MIConvexHullExtensions.MIVector3, DefaultTriangulationCell<MIVector3>>.Create(listPoints, 1e-10);

            List<DefaultTriangulationCell<MIVector3>> listCells = new(tri.Cells.Count());

            List<Mesh3D> listMesh = [];

            Mesh3D mesh = new();

            Dictionary<Geometry.Vector3, int> vertexLookup = [];

            foreach (MIVector3 v in listPoints)
            {
                int iV = mesh.AddVertex(new Vertex3D(v.P));
                vertexLookup[v.P] = iV;
            }

            //mesh.AddVertex(listPoints.Select(p => new Vertex(p.P)).ToList());

            foreach (var cell in tri.Cells)
            {
                Mesh3D faceMesh = new();
                faceMesh.AddVerticies([.. cell.Vertices.Select(fv => new Vertex3D(fv.P))]);

                //For each face, determine if any of the edges are invalid lines.  If all lines are valid then add the face to the output
                bool SkipCell = false;
                foreach (Combo<MIVector3> combo in cell.Vertices.CombinationPairs())
                {
                    LineSegment line = new(combo.A.P.XY(), combo.B.P.XY());

                    PolygonIndex A = cell.Vertices[combo.iA].PolyIndex;
                    PolygonIndex B = cell.Vertices[combo.iB].PolyIndex;

                    if (LineCrossesEmptySpace(A, B, Polygons, line.PointAlongLine(0.5), PolyZ))
                    {
                        SkipCell = true;
                        break;
                    }
                }

                if (SkipCell)
                {
                    //    continue;
                }

                int[][] faceIndicies = [ [0, 1, 2],
                                             [0, 1, 3],
                                             [0, 2, 3],
                                             [1, 2, 3]];


                foreach (int[] face in faceIndicies)
                {
                    //All edges of the triangle must be on the surface or we ignore the face.
                    Geometry.Vector3[] faceVerts = [.. face.Select(i => cell.Vertices[i].P)];
                    int[] faceMeshIndicies = [.. faceVerts.Select(p => vertexLookup[p])];

                    bool FaceOnSurface = true;
                    int OnSurfaceCount = 0;
                    foreach (Combo<int> combo in face.CombinationPairs())
                    {
                        LineSegment line = new(cell.Vertices[combo.A].P.XY(), cell.Vertices[combo.B].P.XY());
                        PolygonIndex A = cell.Vertices[combo.A].PolyIndex;
                        PolygonIndex B = cell.Vertices[combo.B].PolyIndex;

                        bool EmptySpace = LineCrossesEmptySpace(A, B, Polygons, line.PointAlongLine(0.5), PolyZ);
                        if (EmptySpace)
                        {

                            OnSurfaceCount = 0;
                            break;
                        }

                        var EdgeType = EdgeTypeExtensions.GetEdgeType(A, B, Polygons, line.PointAlongLine(0.5));
                        FaceOnSurface &= EdgeType == EdgeType.SURFACE;
                        if (EdgeType == EdgeType.SURFACE)
                            OnSurfaceCount += 1;
                    }

                    //if (FaceOnSurface)
                    if (OnSurfaceCount >= 2)
                    {
                        Face f = new(face);
                        faceMesh.AddFace(f);

                        listMesh.Add(faceMesh);
                        f = new Face(faceMeshIndicies);
                        if (!mesh.Faces.Contains(f))
                            mesh.AddFace(f);
                    }

                    /*
                    if(!FaceOnSurface)
                    {
                        NeedBreak = true; 

                        foreach (Combo<int> combo in face.CombinationPairs())
                        {
                            var line = new LineSegment(cell.Vertices[combo.A].P, cell.Vertices[combo.B].P);
                            PointIndex A = cell.Vertices[combo.A].PolyIndex;
                            PointIndex B = cell.Vertices[combo.B].PolyIndex;

                            if (!surfaceLines.Contains(line))
                            {
                                surfaceLines.Add(line);
                                Colors.Add(GetColorForLine(A, B, Polygons, line.PointAlongLine(0.5)));
                            }
                        }    
                    } 
                    */
                }

                //if(NeedBreak)
                //                    break;


                /*
                       //Wraparound to zero to close the cycle for the face
                       //int next = i + 1 == cell.Vertices.Length ? 0 : i + 1;

                       var line = new LineSegment(cell.Vertices[i].P, cell.Vertices[j].P);
                       PointIndex A = cell.Vertices[i].PolyIndex;
                       PointIndex B = cell.Vertices[j].PolyIndex;

                       bool OnSurface = MeshGraphBuilder.IsLineOnSurface(A, B, Polygons, line.PointAlongLine(0.5));

                       //AllOnSurface &= OnSurface;

                       //if (!AllOnSurface)
                       //{
                       //    //No need to check any other parts of the face
                       //    break;
                       //}

                       //Only add the line if the entire face is on the surface
                       if (!surfaceLines.Contains(line))
                       {
                           surfaceLines.Add(line);
                           Colors.Add(GetColorForLine(A, B, Polygons, line.PointAlongLine(0.5)));
                       }
                   }



               }

               break;

               //if(AllOnSurface)
               //{
               //    surfaceLines.AddRange(FaceLines);
               //    Colors.AddRange(FaceColors);
               //}
               */
            }

            //listMesh.Add(mesh);
            return listMesh;
            /*
            TrianglesView.color = Color.Red;
            TrianglesView.UpdateViews(surfaceLines);
            lineViews = TrianglesView.LineViews.ToArray();

            for (int iLine = 0; iLine < lineViews.Length; iLine++)
            {
                lineViews[iLine].Color = Colors[iLine];
            }
            */
        }

        public static bool LineCrossesEmptySpace(PolygonIndex APoly, PolygonIndex BPoly, Polygon[] Polygons, Geometry.Vector2 midpoint, double[] PolyZ)
        {
            Polygon A = Polygons[APoly.ShapeIndex];
            Polygon B = Polygons[BPoly.ShapeIndex];

            if (APoly.ShapeIndex != BPoly.ShapeIndex)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                if (!midInA && !midInB) //Midpoing not in either polygon.  Passes through empty space that cannot be on the surface
                {
                    return true;
                }

                //If we connect two polygons on the same Z level we know the line crosses empty space
                if (PolyZ[APoly.ShapeIndex] == PolyZ[BPoly.ShapeIndex])
                {
                    return true;
                }
            }
            else
            {
                if (!PolygonIndex.IsBorderLine(APoly, BPoly, A))
                {
                    return true;
                }
            }

            return false;
        }

        private static Color GetColorForLine(PolygonIndex APoly, PolygonIndex BPoly, Polygon[] Polygons, Geometry.Vector2 midpoint)
        {
            Polygon A = Polygons[APoly.ShapeIndex];
            Polygon B = Polygons[BPoly.ShapeIndex];

            if (APoly.ShapeIndex != BPoly.ShapeIndex)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                //lineViews[i].Color = Color.Blue;

                if (!(midInA ^ midInB)) //Midpoint in both or neither polygon. Line may be on exterior surface
                {
                    if (!midInA && !midInB) //Midpoing not in either polygon.  Passes through empty space that cannot be on the surface
                    {
                        return Color.Black.SetAlpha(0.1f); //Exclude from port.  Line covers empty space.  If the triangle contains an intersection point we may need to adjust faces
                        /*
                        if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                        {
                            //Include in port.
                            //Line runs from exterior ring to the far side of an overlapping interior hole
                            lineViews[i].Color = Color.Black.SetAlpha(0.25f); //exclude from port, line covers empty space
                        }
                        else
                        {
                            lineViews[i].Color = Color.White.SetAlpha(0.25f); //Exclude from port.  Line covers empty space
                        }
                        */
                    }
                    else //Midpoing in both polygons.  The line passes through solid space
                    {
                        if (APoly.IsInner ^ BPoly.IsInner) //One or the other vertex is on an interior polygon, but not both
                        {
                            return Color.White.SetAlpha(0.25f); //Exclude. Line from interior polygon to exterior ring through solid space
                        }
                        else
                        {
                            return Color.Orange.SetAlpha(0.25f);  //Exclude. Two interior polygons connected and inside the cells.  Consider using this to vote for branch connection for interior polys
                        }
                    }
                }
                else //Midpoint in one or the other polygon, but not both
                {
                    if (APoly.IsInner ^ BPoly.IsInner) //One or the other is an interior polygon, but not both
                    {
                        if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                        {
                            //Include in port.
                            //Line runs from exterior ring to the near side of an overlapping interior hole
                            return Color.RoyalBlue;
                        }
                        else //Find out if the midpoint is contained by the same polygon with the inner polygon
                        {
                            if ((midInA && APoly.IsInner) || (midInB && BPoly.IsInner))
                            {
                                return Color.Gold;
                            }
                            else
                            {
                                return Color.Pink;
                            }
                        }
                    }
                    else
                    {
                        return Color.Blue;
                    }
                }
            }
            else if (APoly.ShapeIndex == BPoly.ShapeIndex)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = midInA;

                if (PolygonIndex.IsBorderLine(APoly, BPoly, Polygons[APoly.ShapeIndex]))
                {
                    return Color.White;//PolyPointsView[APoly.ShapeIndex].Color;

                }

                if (!midInA)
                {
                    return Color.Black.SetAlpha(0.1f); //Exclude
                }
                else
                {
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.ShapeIndex).Any(p => p.Contains(midpoint));
                    if (APoly.IsInner ^ BPoly.IsInner)
                    {
                        //Two options, the line is outside other shapes or inside other shapes.
                        //If outside other shapes we want to keep this edge, otherwise it is discarded
                        if (!LineIntersectsAnyOtherPoly)
                        {
                            return Color.Green; //Include, standalone faces
                        }
                        else
                        {
                            return Color.Green.SetAlpha(0.1f); //Exclude
                        }
                    }
                    else
                    {
                        if (!LineIntersectsAnyOtherPoly)
                        {
                            return Color.Turquoise;  //Include, standalone faces
                        }
                        else
                        {
                            return Color.Turquoise.SetAlpha(0.1f); //Exclude
                        }
                    }
                }
            }

            return Color.Blue;
        }


        public static void Draw(MonoTestbed window, Scene scene)
        {
        }
    }

    class Delaunay3DTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;

        readonly GamePadStateTracker Gamepad = new();

        VikingXNAGraphics.MeshView<VertexPositionNormalColor> meshView;

        Scene3D Scene;

        LabelView labelCamera;


        bool _initialized = false;
        public bool Initialized => _initialized;

        //Polygons with internal polygon merging with external concavity
        readonly ulong[] TroubleIDS = [
          1333661, //Z = 2
          1333662, //Z = 3
          1333665 //Z =2 
        ];

        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.Scene = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D())
            {
                MaxDrawDistance = 1000000,
                MinDrawDistance = 1
            };
            this.meshView = new MeshView<VertexPositionNormalColor>();

            labelCamera = new LabelView("", new Geometry.Vector2(0, 100));

            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData([.. TroubleIDS.Select(id => (long)id)], true, DataSource.EndpointMap[Endpoint.TEST]);

            AnnotationVizLib.MorphologyNode[] nodes = [.. graph.Nodes.Values];

            Polygon[] polygons = [.. nodes.Select(n => n.Geometry.ToPolygon())];
            double[] polyZ = [.. nodes.Select(n => n.Z)];
            polygons.AddPointsAtAllIntersections();

            DelaunayTetrahedronView tetraView = new(polygons, polyZ);

            Box bbox = tetraView.meshes.First().BoundingBox;

            this.Scene.Camera.Position = (bbox.CenterPoint - new Geometry.Vector3(bbox.Width / 2.0, bbox.Height / 2.0, 0)).ToXNAVector3();
            //this.Scene.Camera.Position = (bbox.CenterPoint * 0.9).ToXNAVector3();
            //this.Scene.Camera.Position = new Vector3(this.Scene.Camera.Position.X, this.Scene.Camera.Position.Y, -this.Scene.Camera.Position.Z);
            //            this.Scene.Camera.Position = bbox.CenterPoint.ToXNAVector3();
            this.Scene.Camera.LookAt = Vector3.Zero;

            //this.Scene.Camera.Position += new Vector3((float)bbox.Width, (float)bbox.Height, (float)bbox.Depth);
            //this.Scene.Camera.Rotation = new Vector3(4.986171f, 1.67181f, 0);
            //this.Scene.Camera.LookAt = meshes.First().BoundingBox.CenterPoint.ToXNAVector3();   


            System.Random r = new();
            foreach (Mesh3D mesh in tetraView.meshes)
            {
                mesh.RecalculateNormals();
                meshView.models.Add(mesh.ToVertexPositionNormalColorMeshModel(new Color((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble())));
            }
            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window)
        {
        }

        /*
        private ICollection<Mesh3D<IVertex3D<ulong>>> RecursivelyGenerateMeshes(AnnotationVizLib.MorphologyGraph graph)
        {
            List<Mesh3D<IVertex3D<ulong>>> listMeshes = new List<Mesh3D<IVertex3D<ulong>>>();

            Mesh3D<IVertex3D<ulong>> structureMesh = MorphologyMesh.SmoothMeshGenerator.Generate(graph);
            if (structureMesh != null)
                listMeshes.Add(structureMesh);

            foreach (var subgraph in graph.Subgraphs.Values)
            {
                listMeshes.AddRange(RecursivelyGenerateMeshes(subgraph));
            }

            return listMeshes;
        }
        */

        public void Update()
        {
            StandardCameraManipulator.Update(this.Scene.Camera);

            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            if (Gamepad.Y_Clicked)
            {
                meshView.WireFrame = !meshView.WireFrame;
            }

            if (Gamepad.A_Clicked)
            {
                this.Scene.Camera.Rotation = Vector3.Zero;
                this.Scene.Camera.Position = new Vector3(0, -10, 0);
            }

            Geometry.Vector3 VolumePosition = Scene.Camera.Position.ToVector3();
            VolumePosition /= new Geometry.Vector3(2.18, 2.18, -90);

            labelCamera.Text = string.Format("{0}\n{1}\n{2}", Scene.Camera.Position, VolumePosition, Scene.Camera.Rotation);

        }

        public void Draw(MonoTestbed window)
        {
            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, float.MaxValue, 0);

            DepthStencilState dstate = new()
            {
                DepthBufferEnable = true,
                StencilEnable = false,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual
            };

            window.GraphicsDevice.DepthStencilState = dstate;
            //window.GraphicsDevice.BlendState = BlendState.Opaque;
            meshView.Draw(window.GraphicsDevice, this.Scene, CullMode.None);

            window.spriteBatch.Begin();
            labelCamera.Draw(window.spriteBatch, window.fontArial, window.Scene);
            window.spriteBatch.End();
        }
    }
}
