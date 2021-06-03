﻿using Geometry;
using Geometry.JSON;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TriangleNet;
using VikingXNA;
using VikingXNAGraphics;


namespace MonogameTestbed
{

    class Delaunay2DTest : IGraphicsTest
    {
        //private static string DebugJSONA = "{\r\n  \"ExteriorRing\": [\r\n    {\r\n      \"X\": 65556.440150145616,\r\n      \"Y\": 37461.394236647327\r\n    },\r\n    {\r\n      \"X\": 65623.795745788157,\r\n      \"Y\": 37340.634756433836\r\n    },\r\n    {\r\n      \"X\": 65481.145457048129,\r\n      \"Y\": 37619.657109034073\r\n    },\r\n    {\r\n      \"X\": 65453.03425733441,\r\n      \"Y\": 37620.828845929325\r\n    },\r\n    {\r\n      \"X\": 65423.55231018005,\r\n      \"Y\": 37620.539564983766\r\n    },\r\n    {\r\n      \"X\": 65230.697691251393,\r\n      \"Y\": 37376.1248026202\r\n    },\r\n    {\r\n      \"X\": 65288.818296405661,\r\n      \"Y\": 37448.20716172336\r\n    },\r\n    {\r\n      \"X\": 65294.323697228123,\r\n      \"Y\": 37454.244785137482\r\n    },\r\n    {\r\n      \"X\": 65307.593301397268,\r\n      \"Y\": 37466.139604615077\r\n    },\r\n    {\r\n      \"X\": 65323.117809354466,\r\n      \"Y\": 37477.594797420978\r\n    },\r\n    {\r\n      \"X\": 65339.989021606634,\r\n      \"Y\": 37488.3714953102\r\n    },\r\n    {\r\n      \"X\": 65357.298738660626,\r\n      \"Y\": 37498.230830037784\r\n    },\r\n    {\r\n      \"X\": 65374.138761023394,\r\n      \"Y\": 37506.933933358756\r\n    },\r\n    {\r\n      \"X\": 65389.600889201785,\r\n      \"Y\": 37514.241937028135\r\n    },\r\n    {\r\n      \"X\": 65402.776923702739,\r\n      \"Y\": 37519.915972800969\r\n    },\r\n    {\r\n      \"X\": 65409.813133387033,\r\n      \"Y\": 37522.691114898967\r\n    },\r\n    {\r\n      \"X\": 65416.49077915887,\r\n      \"Y\": 37525.092403418144\r\n    },\r\n    {\r\n      \"X\": 65422.89954588269,\r\n      \"Y\": 37527.086207041961\r\n    },\r\n    {\r\n      \"X\": 65429.129118422927,\r\n      \"Y\": 37528.638894453929\r\n    },\r\n    {\r\n      \"X\": 65435.269181644006,\r\n      \"Y\": 37529.71683433748\r\n    },\r\n    {\r\n      \"X\": 65441.409420410375,\r\n      \"Y\": 37530.286395376126\r\n    },\r\n    {\r\n      \"X\": 65447.639519586482,\r\n      \"Y\": 37530.313946253322\r\n    },\r\n    {\r\n      \"X\": 65454.049164036733,\r\n      \"Y\": 37529.765855652557\r\n    },\r\n    {\r\n      \"X\": 65460.728038625559,\r\n      \"Y\": 37528.608492257285\r\n    },\r\n    {\r\n      \"X\": 65470.4927750453,\r\n      \"Y\": 37525.925763142237\r\n    },\r\n    {\r\n      \"X\": 65481.08136075714,\r\n      \"Y\": 37521.956384041747\r\n    },\r\n    {\r\n      \"X\": 65492.163831850943,\r\n      \"Y\": 37516.849318173066\r\n    },\r\n    {\r\n      \"X\": 65503.410224416541,\r\n      \"Y\": 37510.753528753419\r\n    },\r\n    {\r\n      \"X\": 65514.490574543743,\r\n      \"Y\": 37503.81797900005\r\n    },\r\n    {\r\n      \"X\": 65525.074918322454,\r\n      \"Y\": 37496.191632130227\r\n    },\r\n    {\r\n      \"X\": 65534.833291842428,\r\n      \"Y\": 37488.023451361143\r\n    },\r\n    {\r\n      \"X\": 65543.43573119356,\r\n      \"Y\": 37479.462399910088\r\n    },\r\n    {\r\n      \"X\": 65550.552272465648,\r\n      \"Y\": 37470.657440994248\r\n    },\r\n    {\r\n      \"X\": 65556.440150145616,\r\n      \"Y\": 37461.394236647327\r\n    }\r\n  ],\r\n  \"InteriorRings\": []\r\n}";
        private static string DebugJSONA = "{\r\n  \"ExteriorRing\": [\r\n  {\r\n    \"X\": 134.72428660436708,\r\n    \"Y\": -109.69079229545241\r\n  },\r\n  {\r\n    \"X\": 168.5224203121179,\r\n    \"Y\": -38.085358362324769\r\n  },\r\n  {\r\n    \"X\": 173.52636094947957,\r\n    \"Y\": -1.028579892757989\r\n  },\r\n  {\r\n    \"X\": 172.23208834991965,\r\n    \"Y\": 17.081788841023808\r\n  },\r\n  {\r\n    \"X\": 168.52242031212518,\r\n    \"Y\": 36.028198576808791\r\n  },\r\n  {\r\n    \"X\": 134.72428660436708,\r\n    \"Y\": 107.63363250994371\r\n  },\r\n  {\r\n    \"X\": 56.6623435746078,\r\n    \"Y\": 161.17835646583626\r\n  },\r\n  {\r\n    \"X\": -17.66022361501382,\r\n    \"Y\": 170.75336333838641\r\n  },\r\n  {\r\n    \"X\": -92.191801055138058,\r\n    \"Y\": 144.01956497065839\r\n  },\r\n  {\r\n    \"X\": -108.21206728393736,\r\n    \"Y\": 133.24556159282656\r\n  },\r\n  {\r\n    \"X\": -121.93322075722244,\r\n    \"Y\": 121.35478598323607\r\n  },\r\n  {\r\n    \"X\": -144.5979997446484,\r\n    \"Y\": 91.613366281151684\r\n  },\r\n  {\r\n    \"X\": -171.33179811237642,\r\n    \"Y\": 17.081788841023808\r\n  },\r\n  {\r\n    \"X\": -167.62213007457467,\r\n    \"Y\": -38.085358362317493\r\n  },\r\n  {\r\n    \"X\": -133.82399636681657,\r\n    \"Y\": -109.69079229544877\r\n  },\r\n  {\r\n    \"X\": -121.93322075722244,\r\n    \"Y\": -123.41194576875205\r\n  },\r\n  {\r\n    \"X\": -55.762053337071848,\r\n    \"Y\": -163.23551625135588\r\n  },\r\n  {\r\n    \"X\": 56.662343574622355,\r\n    \"Y\": -163.23551625135588\r\n  },\r\n  {\r\n    \"X\": 134.72428660436708,\r\n    \"Y\": -109.69079229545241\r\n  }\r\n],\r\n  \"InteriorRings\": []\r\n}";


        Scene scene;
        PointSetViewCollection Points_A = new PointSetViewCollection(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        PointSetViewCollection Points_B = new PointSetViewCollection(Color.Red, Color.Pink, Color.Plum);
        PointSetViewCollection Points_C = new PointSetViewCollection(Color.Red, Color.Pink, Color.GreenYellow);

        PolygonContourView PolyAView = new PolygonContourView();
        PolygonContourView PolyBView = new PolygonContourView();
        PolygonContourView PolyCView = new PolygonContourView();

        GamePadStateTracker Gamepad = new GamePadStateTracker();
        Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();

        GridVector2 Cursor = GridVector2.Zero;
        CircleView cursorView;
        LabelView cursorLabel;

        public double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public string Title => this.GetType().Name;

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            PolyAView.Color = Color.Red;
            PolyBView.Color = Color.Blue;
            PolyCView.Color = Color.Green;

            PolyCView.Polygon = GeometryJSONExtensions.PolygonFromJSON(DebugJSONA);
            scene.Camera.LookAt = PolyCView.Polygon.ExteriorRing[0].ToXNAVector2();

            cursorView = new CircleView(new GridCircle(Cursor, PointRadius), Color.Gray);

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));

        }

        public void UnloadContent(MonoTestbed window)
        {
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
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor);
                cursorLabel.FontSize = 2;
                cursorLabel.Color = Color.Yellow;
            }

            if (state.Buttons.RightStick == ButtonState.Pressed)
            {
                Cursor = this.scene.Camera.LookAt.ToGridVector2();
                cursorView = new CircleView(new GridCircle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor);
                cursorLabel.FontSize = 2;
                cursorLabel.Color = Color.Yellow;
            }

            if (Gamepad.A_Clicked)
            {
                Points_A.TogglePoint(Cursor);
                if (Points_A.Points.Count >= 3)
                {
                    PolyAView.Polygon = new GridPolygon(Points_A.Points.Points.EnsureClosedRing());
                }
            }

            if (Gamepad.B_Clicked)
            {
                Points_B.TogglePoint(Cursor);
                if (Points_B.Points.Count >= 3)
                {
                    PolyBView.Polygon = new GridPolygon(Points_B.Points.Points.EnsureClosedRing());
                }
            }

            if (Gamepad.Y_Clicked)
            {
                Points_C.TogglePoint(Cursor);
                if (Points_C.Points.Count >= 3)
                {
                    PolyCView.Polygon = new GridPolygon(Points_C.Points.Points.EnsureClosedRing());
                }
            }
        }

        public void Draw(MonoTestbed window)
        {
            if (cursorView != null)
                CircleView.Draw(window.GraphicsDevice, this.scene, OverlayStyle.Alpha, new CircleView[] { cursorView });
             
            Points_A.Draw(window, scene);
            Points_B.Draw(window, scene);
            Points_C.Draw(window, scene);

            PolyAView.Draw(window);
            PolyBView.Draw(window);
            PolyCView.Draw(window);

            if (cursorLabel != null)
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
