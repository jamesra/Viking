using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Threading.Tasks;
using TriangleNet;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;


namespace MonogameTestbed
{

    class TriangleAlgorithmTest : IGraphicsTest
    {
        readonly TestInputContext Input = new();
        public string Title => this.GetType().Name;
        Scene scene;
        readonly PointSetViewCollection Points_A = new(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        readonly PointSetViewCollection Points_B = new(Color.Red, Color.Pink, Color.Plum);
        readonly PointSetViewCollection Points_C = new(Color.Red, Color.Pink, Color.GreenYellow);
        readonly UntiledRegionView PolyBorderView = new();
        Geometry.Vector2 Cursor;
        CircleView cursorView;
        LabelView cursorLabel;

        public double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized => _initialized;

        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Input.UpdateTrackers();

            PolyBorderView.AddSet(Points_A.Points);
            PolyBorderView.AddSet(Points_B.Points);
            PolyBorderView.AddSet(Points_C.Points);
            PolyBorderView.Color = Color.Yellow;
            PolyBorderView.DelaunayView.color = Color.Gray;
            PolyBorderView.BoundaryView.color = Color.Yellow;
            PolyBorderView.VoronoiView.color = Color.DarkRed;

            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window) => this.scene.SaveCamera(TestMode.TRIANGLEALGORITHM);

        public void Update()
        {
            GamePadState state = Input.Update(scene);

            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToVector2();
                cursorView = new CircleView(new Circle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = 2,
                    Color = Color.Yellow
                };
            }

            if (state.Buttons.RightStick == ButtonState.Pressed)
            {
                Cursor = this.scene.Camera.LookAt.ToVector2();
                cursorView = new CircleView(new Circle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = 2,
                    Color = Color.Yellow
                };
            }

            if (Input.Gamepad.A_Clicked)
            {
                Points_A.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_A.Points, 0);
            }

            if (Input.Gamepad.B_Clicked)
            {
                Points_B.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_B.Points, 1);
            }

            if (Input.Gamepad.Y_Clicked)
            {
                Points_C.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_C.Points, 2);
            }
        }

        public void Draw(MonoTestbed window)
        {
            if (cursorView != null)
                CircleView.Draw(window.GraphicsDevice, this.scene, OverlayStyle.Alpha, new CircleView[] { cursorView });

            PolyBorderView.Draw(window, scene);

            Points_A.Draw(window, scene);
            Points_B.Draw(window, scene);
            Points_C.Draw(window, scene);

            if (cursorLabel != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, this.scene, new LabelView[] { cursorLabel });
        }

        /*
        private DynamicRenderMesh<int> ToMesh(TriangleNet.Topology.DCEL.DcelMesh mesh)
        { 
            DynamicRenderMesh<int> DRMesh = new DynamicRenderMesh<int>();

            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();

            DRMesh.AddVertex(mesh.Vertices.Select(v => new Vertex<int>(new Geometry.Vector3(v.X, v.Y, 0), Geometry.Vector3.Zero, v.ID)).ToArray());

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
