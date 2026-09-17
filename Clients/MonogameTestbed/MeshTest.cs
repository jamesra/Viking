using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    class MeshTest : IGraphicsTest
    {
        readonly TestInputContext Input = new();
        public string Title => this.GetType().Name;
        MeshView<VertexPositionColor> meshView;
        MeshView<VertexPositionNormalColor> meshViewWithLighting;
        Mesh3D tetraMesh;
        Mesh3D cubeMesh;

        Scene3D Scene;

        LabelView labelCamera;


        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        private Vertex3D[] CreateCubeVerts(Geometry.Vector3 offset)
        {
            Vertex3D[] verts = new Vertex3D[] {
                new Vertex3D(new Geometry.Vector3(-5, -5, -5), Geometry.Vector3.Zero),
                new Vertex3D(new Geometry.Vector3(5, -5, -5), Geometry.Vector3.Zero),
                new Vertex3D(new Geometry.Vector3(-5, -5, 5), Geometry.Vector3.Zero),
                new Vertex3D(new Geometry.Vector3(5, -5, 5), Geometry.Vector3.Zero),
                new Vertex3D(new Geometry.Vector3(-5, 5, -5), Geometry.Vector3.Zero),
                new Vertex3D(new Geometry.Vector3(5, 5, -5), Geometry.Vector3.Zero),
                new Vertex3D(new Geometry.Vector3(-5, 5, 5), Geometry.Vector3.Zero),
                new Vertex3D(new Geometry.Vector3(5, 5, 5), Geometry.Vector3.Zero)
            };

            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Position += offset;
            }

            return verts;
        }

        private Face[] CreateCubeFaces()
        {
            // Triangles only — RecalculateNormals and the GPU path require triangular faces.
            return new Face[] {
                new Face(0, 2, 3), new Face(0, 3, 1),       // Bottom
                new Face(0, 1, 5), new Face(0, 5, 4),       // Front
                new Face(4, 6, 7), new Face(4, 7, 5),       // Top
                new Face(2, 6, 7), new Face(2, 7, 3),       // Back
                new Face(1, 3, 7), new Face(1, 7, 5),       // Right
                new Face(0, 4, 6), new Face(0, 6, 2) };      // Left
        }

    private Vertex3D[] CreateTetrahedronVerts(Geometry.Vector3 offset)
    {
        Vertex3D[] verts = new Vertex3D[] {new Vertex3D(new Geometry.Vector3(0, 0, 0), new Geometry.Vector3(0, 0, 0)),
                                 new Vertex3D(new Geometry.Vector3(0, 1, 0), new Geometry.Vector3(0, 1, 0)),
                                 new Vertex3D(new Geometry.Vector3(0, 0, 1), new Geometry.Vector3(0, 0, 1)),
                                 new Vertex3D(new Geometry.Vector3(1, 0, 0), new Geometry.Vector3(1, 0, 0)) };

        for(int i = 0; i < verts.Length; i++)
        {
            verts[i].Position += offset; 
        }

        return verts;
    }

    private Face[] CreateTetrahedronFaces()
    {
        return new Face[] {new Face(0,1,2),
                           new Face(0,3,1),
                           new Face(0,2,3),
                           new Face(1,3,2) };
    }

    private Mesh3D CreateTetrahedronMeshModel(Geometry.Vector3 offset)
    {
        Mesh3D mesh = new Mesh3D();
        mesh.AddVerticies(CreateTetrahedronVerts(offset));
        Face[] faces = CreateTetrahedronFaces();
        foreach (Face f in faces)
        {
            mesh.AddFace(f);
        }

        return mesh;
    }

    private Mesh3D CreateCubeMeshModel(Geometry.Vector3 offset)
    {
        Mesh3D mesh = new Mesh3D();
        mesh.AddVerticies(CreateCubeVerts(offset));
        Face[] faces = CreateCubeFaces();
        foreach (Face f in faces)
        {
            mesh.AddFace(f);
        }

        return mesh;
    }
       
    public Task Init(MonoTestbed window)
    {
        this.Scene = new Scene3D(window.GraphicsDevice.Viewport, new Camera3D());
        this.meshView = new MeshView<VertexPositionColor>();
        this.meshViewWithLighting = new MeshView<VertexPositionNormalColor>();

        this.Scene.Camera.LookAt = Vector3.Zero;
        this.Scene.Camera.Position = new Vector3(0, -0, -65);

        this.Scene.MaxDrawDistance = 10000;

      Color[] tetra_colors = new Color[] { Color.Red, Color.Blue, Color.Green, Color.Yellow };
      Color[] cube_colors  = new Color[] { Color.White, Color.Blue, Color.Green, Color.Yellow, Color.Red, Color.Orange, Color.Purple, Color.Black };

      tetraMesh = CreateTetrahedronMeshModel(new Geometry.Vector3(-20, 0, 0));

      Mesh3D cubeMeshForView = CreateCubeMeshModel(new Geometry.Vector3(20, 0, 0));
      cubeMeshForView.RecalculateNormals();
      MeshModel<VertexPositionNormalColor> cubeModel = cubeMeshForView.ToVertexPositionNormalColorMeshModel(cube_colors);
      meshViewWithLighting.models.Add(cubeModel);

      MeshModel<VertexPositionColor> tetraModel = tetraMesh.ToVertexPositionColorMeshModel(tetra_colors);
      meshView.models.Add(tetraModel);

      // Disc, second box, polygon slab and circle hull require ShapeMeshGenerator (not in codebase)
      // MeshModel<VertexPositionNormalColor> discModel = ...
      // MeshModel<VertexPositionNormalColor> boxModel = ...
      // MeshModel<VertexPositionNormalColor> polyModel = ...
      // MeshModel<VertexPositionNormalColor> circleModel = BuildCircleConvexHull(...);

      this.Scene.Camera.Position = new Vector3(29, -13.5f, 24.75f);
      this.Scene.Camera.Rotation = new Vector3(2.5f, 2.055f, 0);

        // BuildSmoothMesh* methods require StandardModels and MorphologyMesh.SmoothMeshGenerator (not available)
        // foreach (var model in BuildSmoothMeshTwoNonOverlappingCircles(...)) ...
        // foreach (var model in BuildSmoothMeshCircleBranchOfOneOverlapping(...)) ...
        // foreach (var model in BuildSmoothMeshLine(...)) ...


    //meshViewWithLighting.models.Add(BuildPolygonBranchCenter(Geometry.Vector3.Zero));
    //Add a simple shape that should always be correct to test simple process and terminal rendering
    //meshViewWithLighting.models.Add(BuildSmoothMeshTwoNonOverlappingCircles(new Geometry.Vector3(50,0,0)));

    //meshViewWithLighting.models.Add(BuildSmoothMeshTwoPolygons(Geometry.Vector3.Zero));
    //meshViewWithLighting.models.Add(BuildPolygonBranchCenter(Geometry.Vector3.Zero));

    //meshView.models.Add(BuildSmoothMeshFromSharedModel_ColorOnly(new Geometry.Vector3(-25, 0, 0)));

    //meshViewWithLighting.models.Add(BuildSmoothMeshFromSharedModel(new Geometry.Vector3(0, 0, 0)));
    
      labelCamera = new LabelView("", new Geometry.Vector2(39950, 0));

      _initialized = true;
      return Task.CompletedTask;
}

public void UnloadContent(MonoTestbed window)
{
    //this.Scene.SaveCamera(TestMode.MESH);
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMesh1(Geometry.Vector3 translate)
{
    // StandardModels / SmoothMeshGenerator not available
    return null;
}

private MeshModel<VertexPositionColor> BuildSmoothMeshFromSharedModel_ColorOnly(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshFromSharedModel(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshTwoPolygons(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshTwoNonOverlappingCircles(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleBranchOfOneOverlapping(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleBranchOfOneOverlappingButTall(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleXBranchOfOneOverlappingButTall(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshCircleDoubleBranchOfOneOverlappingButTall(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildSmoothMeshLine(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildPolygonBranchCenter(Geometry.Vector3 translate)
{
    return null;
}

private MeshModel<VertexPositionNormalColor> BuildCircleConvexHull(ICircle2D circle)
{
    // ShapeMeshGenerator not in codebase; circle hull disabled
    return null;
}

public void Update()
{
    StandardCameraManipulator.Update(this.Scene.Camera);
    GamePadState state = Input.UpdateTrackers();

    if (Input.Gamepad.Y_Clicked)
    {
        meshView.WireFrame = !meshView.WireFrame;
        meshViewWithLighting.WireFrame = meshView.WireFrame;
    }

    labelCamera.Text = string.Format("{0} {2}", Scene.Camera.Position, Scene.Camera.LookAt, Scene.Camera.Rotation);
}

public void Draw(MonoTestbed window)
{
    this.Scene.Viewport = window.GraphicsDevice.Viewport;
    window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, MonoTestbed.DefaultBackground, 1.0f, 0);

    DepthStencilState dstate = new DepthStencilState
    {
        DepthBufferEnable = true,
        StencilEnable = false,
        DepthBufferWriteEnable = true,
        DepthBufferFunction = CompareFunction.LessEqual
    };

    RasterizerState rState = new RasterizerState();
    rState.CullMode = CullMode.CullClockwiseFace;
    rState.DepthClipEnable = true;
    rState.FillMode = FillMode.Solid;
    window.GraphicsDevice.DepthStencilState = dstate;
    //window.GraphicsDevice.BlendState = BlendState.Opaque;
    meshView.Draw(window.GraphicsDevice, this.Scene, CullMode.CullCounterClockwiseFace);
    meshViewWithLighting.Draw(window.GraphicsDevice, this.Scene, CullMode.CullCounterClockwiseFace);

    window.spriteBatch.Begin();
    labelCamera.Draw(window.spriteBatch, window.fontArial, window.Scene);
    window.spriteBatch.End(); 
}


}
}
