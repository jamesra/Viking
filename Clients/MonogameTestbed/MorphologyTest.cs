using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    class MorphologyTest : IGraphicsTest
    {
        readonly TestInputContext Input = new();
        public string Title => this.GetType().Name;

        VikingXNAGraphics.MeshView<VertexPositionNormalColor> meshView;

        ICollection<Mesh3D<IVertex3D<ulong>>> meshes;

        Scene3D Scene;

        LabelView labelCamera;




        bool _initialized = false;
        public bool Initialized => _initialized;


        //        long[] TroubleIDS = new long[] {
        //            //5868, //Z: 231
        //            //5872, //Z: 232
        ////            6085,   //Z: 235
        //            //1333634, //Z: 235
        //            1026126, //Z: 234
        //            1026127, //Z: 233
        //            //1026128,  //Z: 232
        //            //1026129, //Z: 231
        //            };

        /// <summary>
        /// RPC1 Glial cell 
        /// </summary>
        /*long[] TroubleIDS = new long[] {
            100075, //Z: 234
            100076, //Z: 233
            };
            */

        /*
        long[] TroubleIDS = new long[] {
            //58691, //Z: 234
            58692, //Z: 233
            58694
            };
        */
        /*
        long[] TroubleIDS = new long[] {
            82701, //Z: 234
            82881, //Z: 233
            82882,
            82883
            };*/

        /*
    long[] TroubleIDS = new long[] {
      //  58664,
        58666,
        58668,

    };*/
        /*
        //Polygons with internal polygon
        long[] TroubleIDS = new long[] {
          //  58664,
            82612, //Z: 756
            82617, //Z: 757 Small Branch
            82647, //Z: 757
            82679, //Z: 758
            82620, //Z: 758 Small Branch

        };
        */
        /*
        //Polygons with internal polygon merging with external concavity
        long[] TroubleIDS = new long[] {
          //  58664,
            82884, //Z: 767
            82908, //Z: 768

        };
        */

        //Polygons with internal polygon merging with external concavity
        readonly long[] TroubleIDS = [
          1333661, //Z = 2
          1333662, //Z = 3
          1333665 //Z =2

        ];

        public Task Init(MonoTestbed window)
        {
            this.Scene = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D())
            {
                MaxDrawDistance = 1000000,
                MinDrawDistance = 1
            };
            this.meshView = new MeshView<VertexPositionNormalColor>();

            labelCamera = new LabelView("", new Geometry.Vector2(0, 100));

            //meshes = InitSmallSmoothModelFromOData(144287, ENDPOINT.TEST);
            //meshes = InitSmallSmoothModelFromOData(new long[] { 1 }, ENDPOINT.RC2);
            //meshes = InitSmallSmoothModelFromOData(476, ENDPOINT.RC1);
            //meshes = InitSmallSmoothModelFromOData(new long[] { 476 }, ENDPOINT.TEST);
            //meshes = InitSmallSmoothModelFromOData(new long[] { 144302 }, ENDPOINT.TEST);
            //meshes = InitSmallSmoothModelFromOData(476, ENDPOINT.RC1);
            //meshes = InitSmallSmoothModelFromOData(new long[] { 192 }, ENDPOINT.RPC1);
            meshes = InitSmallSmoothModelFromOData([2713], Endpoint.RPC1) ?? [];
            //meshes = InitSmallSmoothModelFromOData(5554, ENDPOINT.RC2);
            //meshes = InitSmallSmoothModelFromOData(new long[] { 1650, 858 }, ENDPOINT.INFERIORMONKEY);

            //Bad polygon Location #1026126.  Position X: 62134.0	Y: 51034.8	Z: 234	DS: 1.97
            //meshes = InitSmallSmoothModelFromOData(new long[] { 180 }, ENDPOINT.TEST);
            //meshes = InitSmallSmoothModelFromOData(new long[] { 1228 }, ENDPOINT.RPC1); //Bipolor cell that crashes current live exporter 2/15/18
            //meshes = InitSmallSmoothModelFromOData(new long[] { 2628 }, ENDPOINT.RPC1); //Glial cell
            //meshes = InitSmallSmoothModelFromOData(new long[] { 192 }, ENDPOINT.RPC1); //Bipolar with circle->Poly problems


            //meshes = InitSmallSmoothModelFromODataLocations(TroubleIDS, ENDPOINT.RPC1);
            //meshes = InitSmallSmoothModelFromODataLocations(TroubleIDS, ENDPOINT.TEST);

            //meshes = InitSmallSmoothModelFromOData(207, ENDPOINT.TEMPORALMONKEY);
            //meshes = InitSmallModelFromOData(476);
            //meshes = InitSmallModelFromOData(1);

            if (meshes.Count == 0)
            {
                const string message = "MorphologyTest has no meshes to display (SmoothMeshGenerator is unavailable). Use BajajMultiTest for contour-to-mesh.";
                Console.WriteLine(message);
                throw new InvalidOperationException(message);
            }

            Box bbox = meshes.First().BoundingBox;

            this.Scene.Camera.Position = (bbox.CenterPoint - new Geometry.Vector3(bbox.Width / 2.0, bbox.Height / 2.0, 0)).ToXNAVector3();
            //this.Scene.Camera.Position = (bbox.CenterPoint * 0.9).ToXNAVector3();
            //this.Scene.Camera.Position = new Vector3(this.Scene.Camera.Position.X, this.Scene.Camera.Position.Y, -this.Scene.Camera.Position.Z);
            //            this.Scene.Camera.Position = bbox.CenterPoint.ToXNAVector3();
            this.Scene.Camera.LookAt = Vector3.Zero;

            //this.Scene.Camera.Position += new Vector3((float)bbox.Width, (float)bbox.Height, (float)bbox.Depth);
            //this.Scene.Camera.Rotation = new Vector3(4.986171f, 1.67181f, 0);
            //this.Scene.Camera.LookAt = meshes.First().BoundingBox.CenterPoint.ToXNAVector3();            

            System.Random r = new();
            foreach (Mesh3D<IVertex3D<ulong>> mesh in meshes)
            {
                meshView.models.Add(mesh.ToVertexPositionNormalColorMeshModel(new Color((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble())));
            }
            _initialized = true;
            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window)
        {
            //this.scene.SaveCamera(TestMode.MESH);
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public static ICollection<Mesh3D<IVertex3D<ulong>>> InitSmallTopologyModelFromOData(ulong CellID, Endpoint endpoint)
        {
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData(new long[] { (long)CellID }, true, DataSource.EndpointMap[endpoint]);

            // TODO: TopologyMeshGenerator not available - need to implement or find alternative
            // MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            // return new Mesh3D<IVertex3D<ulong>>[] { MorphologyMesh.TopologyMeshGenerator.Generate(graph.Subgraphs.Values.First()) };
            return Array.Empty<Mesh3D<IVertex3D<ulong>>>();
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public ICollection<Mesh3D<IVertex3D<ulong>>> InitSmallSmoothModelFromOData(long[] CellIDs, Endpoint endpoint)
        {
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData([.. CellIDs.Select(id => id)], true, DataSource.EndpointMap[endpoint]);

            //SelectZRange(graph, 231, 235);
            //SelectSubsetOfIDs(graph, TroubleIDS);

            AnnotationVizLib.MorphologyGraph.SmoothProcesses(graph);

            //MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return RecursivelyGenerateMeshes(graph);
        }

        /// <summary>
        /// Create a tube of circles offset slighty each section
        /// </summary>
        public ICollection<Mesh3D<IVertex3D<ulong>>> InitSmallSmoothModelFromODataLocations(long[] LocationIDs, Endpoint endpoint)
        {
            AnnotationVizLib.MorphologyGraph graph = AnnotationVizLib.OData.ODataMorphologyFactory.FromOData([.. LocationIDs.Select(id => id)], true, DataSource.EndpointMap[endpoint]);

            //MorphologyMesh.TopologyMeshGenerator generator = new MorphologyMesh.TopologyMeshGenerator();
            return RecursivelyGenerateMeshes(graph);
        }

        private static void SelectZRange(AnnotationVizLib.MorphologyGraph graph, int StartZ, int EndZ)
        {
            AnnotationVizLib.MorphologyNode[] nodes = [.. graph.Nodes.Values.Where(n => n.UnscaledZ < StartZ || n.UnscaledZ > EndZ)];

            for (int i = 0; i < nodes.Length; i++)
            {
                graph.RemoveNode(nodes[i].Key);
            }

            foreach (AnnotationVizLib.MorphologyGraph subgraph in graph.Subgraphs.Values)
            {
                SelectZRange(subgraph, StartZ, EndZ);
            }

            return;
        }

        private static void SelectSubsetOfIDs(AnnotationVizLib.MorphologyGraph graph, long[] IDs)
        {
            AnnotationVizLib.MorphologyNode[] nodes = [.. graph.Nodes.Values.Where(n => !IDs.Contains((long)n.Key))];

            for (int i = 0; i < nodes.Length; i++)
            {
                graph.RemoveNode(nodes[i].Key);
            }

            foreach (AnnotationVizLib.MorphologyGraph subgraph in graph.Subgraphs.Values)
            {
                SelectSubsetOfIDs(subgraph, IDs);
            }

            return;
        }


        private static ICollection<Mesh3D<IVertex3D<ulong>>> RecursivelyGenerateMeshes(AnnotationVizLib.MorphologyGraph graph)
        {
            List<Mesh3D<IVertex3D<ulong>>> listMeshes = [];

            // TODO: SmoothMeshGenerator not available - need to implement or find alternative
            // Mesh3D<IVertex3D<ulong>> structureMesh = MorphologyMesh.SmoothMeshGenerator.Generate(graph);
            // if(structureMesh != null)
            //     listMeshes.Add(structureMesh);

            foreach (var subgraph in graph.Subgraphs.Values)
            {
                listMeshes.AddRange(RecursivelyGenerateMeshes(subgraph));
            }

            return listMeshes;
        }

        public void Update()
        {
            StandardCameraManipulator.Update(this.Scene.Camera);

            GamePadState state = Input.UpdateTrackers();

            if (Input.Gamepad.Y_Clicked)
            {
                meshView.WireFrame = !meshView.WireFrame;
            }

            if (Input.Gamepad.A_Clicked)
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
            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, MonoTestbed.DefaultBackground, 1.0f, 0);

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
