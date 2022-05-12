using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Threading.Tasks;
using TriangleNet;
using VikingXNA;
using VikingXNAGraphics;


namespace MonogameTestbed
{

    class TriangleAlgorithmTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        Scene scene;
        readonly PointSetViewCollection Points_A = new PointSetViewCollection(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        readonly PointSetViewCollection Points_B = new PointSetViewCollection(Color.Red, Color.Pink, Color.Plum);
        readonly PointSetViewCollection Points_C = new PointSetViewCollection(Color.Red, Color.Pink, Color.GreenYellow);
        readonly UntiledRegionView PolyBorderView = new UntiledRegionView();
        readonly GamePadStateTracker Gamepad = new GamePadStateTracker();
        readonly Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();

        GridVector2 Cursor;
        CircleView cursorView;
        LabelView cursorLabel; 

        public double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));

            PolyBorderView.AddSet(Points_A.Points);
            PolyBorderView.AddSet(Points_B.Points);
            PolyBorderView.AddSet(Points_C.Points);
            PolyBorderView.Color = Color.Yellow;
            PolyBorderView.DelaunayView.color = Color.Gray;
            PolyBorderView.BoundaryView.color = Color.Yellow;
            PolyBorderView.VoronoiView.color = Color.DarkRed;

            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window)
        {
            this.scene.SaveCamera(TestMode.MESH);
        }

        public void Update()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state); 

            CameraManipulator.Update(scene.Camera);
             
            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToGridVector2();
                cursorView = new CircleView(new GridCircle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = 2,
                    Color = Color.Yellow
                };
            }

            if (state.Buttons.RightStick == ButtonState.Pressed)
            {
                Cursor = this.scene.Camera.LookAt.ToGridVector2();
                cursorView = new CircleView(new GridCircle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = 2,
                    Color = Color.Yellow
                };
            }

            if (Gamepad.A_Clicked)
            {
                Points_A.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_A.Points, 0);
            }

            if (Gamepad.B_Clicked)
            {
                Points_B.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_B.Points, 1);
            }

            if (Gamepad.Y_Clicked)
            {
                Points_C.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_C.Points, 2);
            }
        }

        public void Draw(MonoTestbed window)
        {
            if(cursorView != null)
                CircleView.Draw(window.GraphicsDevice, this.scene, OverlayStyle.Alpha, new CircleView[] { cursorView });
             
            PolyBorderView.Draw(window, scene);

            Points_A.Draw(window, scene);
            Points_B.Draw(window, scene);
            Points_C.Draw(window, scene);

            if(cursorLabel != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, this.scene, new LabelView[] { cursorLabel });
        }

        /*
        private DynamicRenderMesh<int> ToMesh(TriangleNet.Topology.DCEL.DcelMesh mesh)
        { 
            DynamicRenderMesh<int> DRMesh = new DynamicRenderMesh<int>();

            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();

            DRMesh.AddVertex(mesh.Vertices.Select(v => new Vertex<int>(new GridVector3(v.X, v.Y, 0), GridVector3.Zero, v.ID)).ToArray());

            foreach(TriangleNet.Topology.DCEL.Face f in mesh.Faces)
            {
                if (!f.Bounded)
                    continue;

                List<int> faceIDs = new List<int>(4);
                foreach(var edge in f.EnumerateEdges())
                {
                    faceIDs.Add(edge.Origin.ID);
                    System.Diagnostics.Debug.Assert(faceIDs.Count <= 4);
                }

                Face newFace = new Face(faceIDs);
                DRMesh.AddFace(newFace);
            }

            return DRMesh;
        }
        */

        
    }
}
