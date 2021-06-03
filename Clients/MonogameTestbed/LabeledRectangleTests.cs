using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using VikingXNA;
using VikingXNAGraphics;


namespace MonogameTestbed
{
    /// <summary>
    /// Draws labels and shapes on a uniform grid.  Useful to ensure that shapes render at the correct size
    /// 
    /// Keys:
    /// N = Toggle newlines in the text
    /// 
    /// </summary>
    class LabeledRectangleTests : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        /// <summary>
        /// When true we draw two lines of text to ensure the layout is reasonable
        /// </summary>
        public bool UseNewlines = true;

        /// <summary>
        /// If true we pass a rectangle to the LabeledRectangle instead of a point position
        /// </summary>
        public bool PassRectanglesToConstructor = true;
         
        /// <summary>
        /// Labels that are displayed on the grid and should change size and position on the screen as the camera changes
        /// </summary>
        List<LabeledRectangleView> views = new List<LabeledRectangleView>();

        /// <summary>
        /// The background grid used to provide scale and visual proof that rectangles have proper dimensions
        /// </summary>
        List<CircleView> dot_views = new List<CircleView>();

        /// <summary>
        /// A direct rectangle view shown in the corner to verify that Rectangle Views have correct dimensions
        /// </summary>
        List<RectangleView> rect_views = new List<RectangleView>();

        /// <summary>
        /// Rectangle labels that are positioned in screen coordinates and should not change size or scale with camera movements
        /// I did not get fixed labels working using a separate viewport and moved on.
        /// </summary>
        List<LabeledRectangleView> fixed_views = new List<LabeledRectangleView>();

        GamePadStateTracker Gamepad = new GamePadStateTracker();
        KeyboardStateTracker Keypad = new KeyboardStateTracker();
        Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();

        Scene scene;

        /// <summary>
        /// Used as a fixed viewport size to position labels
        /// </summary>
        Scene screen_space_scene;

        public void Init(MonoTestbed window)
        {
            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Viewport screen_space_viewport = new Viewport(-1, -1, 2, 2);
            this.screen_space_scene = new Scene(window.GraphicsDevice.Viewport, new Camera());
            screen_space_scene.Viewport = screen_space_viewport;

            LayoutButtons();
            LayoutFixedButtons();
            LayoutRectangle();

            int StepSize = 10; 

            if (dot_views.Count == 0)
            {
                for (int x = -150; x <= 150; x += StepSize)
                {
                    for (int y = -150; y <= 150; y += StepSize)
                    {
                        Color color = x == 0 || y == 0 ? Color.Yellow : x % (StepSize * 5) == 0 || y % (StepSize * 5) == 0 ? Color.Green :  Color.Red;
                        var dot = new CircleView(new GridCircle(x, y, 1), color);
                        dot_views.Add(dot);
                    }
                }
            }

            this._initialized = true;
        }

        private void LayoutButtons()
        {   
            GridVector2 box_size = new GridVector2(50, 25); //Size of buttons
            GridVector2 half_box_size = box_size / 2;
            GridVector2 spacing = new GridVector2(100, 50); //Space between buttons on grid

            HorizontalAlignment hAlign = HorizontalAlignment.CENTER;
            VerticalAlignment vAlign = VerticalAlignment.CENTER;

            views.Clear();

            int x = 0;
            int y = 0;

            
            for (x = -1; x <= 1; x++)
            {
                hAlign = x < 0 ? HorizontalAlignment.LEFT : x == 0 ? HorizontalAlignment.CENTER : HorizontalAlignment.RIGHT;

                for (y = -1; y <= 1; y++)
                {
                    vAlign = y < 0 ? VerticalAlignment.BOTTOM : y == 0 ? VerticalAlignment.CENTER : VerticalAlignment.TOP;

                    Anchor anchor = new Anchor { Horizontal = hAlign, Vertical = vAlign };
                    GridVector2 origin = ((spacing) * new GridVector2(x, y));// - half_box_size;
                    GridRectangle bbox = new GridRectangle(origin, box_size.X, box_size.Y);

                    string label_text = UseNewlines ? string.Format("{0}\n{1}\nGoldfish", vAlign, hAlign) : string.Format("{0} {1}", vAlign, hAlign);
                    LabeledRectangleView new_view;

                    if (PassRectanglesToConstructor)
                    {
                        new_view = new LabeledRectangleView(label_text, bbox, Color.Black, Color.Black.Random().SetAlpha(0.5f), alignment: new Alignment { Horizontal = hAlign, Vertical = vAlign }, fontSize: 10);
                    }
                    else
                    {
                        new_view = new LabeledRectangleView(label_text, origin, Color.Black, Color.Black.Random().SetAlpha(0.5f), anchor: anchor, alignment: new Alignment { Horizontal = hAlign, Vertical = vAlign }, fontSize: 10);
                    }

                    views.Add(new_view); 
                }
            } 
            
        }

        private void LayoutFixedButtons()
        {
            GridVector2 box_size = new GridVector2(0.1, 0.05); //Size of buttons
            GridVector2 half_box_size = box_size / 2;
            GridVector2 spacing = new GridVector2(0.5, 0.5); //Space between buttons on grid

            fixed_views.Clear();

            HorizontalAlignment hAlign = HorizontalAlignment.CENTER;
            VerticalAlignment vAlign = VerticalAlignment.CENTER;
             
            int x = 0;
            int y = 0;

            //Place a fixed view at each corner
            for (x = -1; x <= 1; x++)
            {
                hAlign = x < 0 ? HorizontalAlignment.LEFT : x == 0 ? HorizontalAlignment.CENTER : HorizontalAlignment.RIGHT;

                for (y = -1; y <= 1; y++)
                {
                    /*if (x == 0 && y == 0)
                    {
                        continue; 
                    }
                    */

                    vAlign = y < 0 ? VerticalAlignment.BOTTOM : y == 0 ? VerticalAlignment.CENTER : VerticalAlignment.TOP;

                    Anchor anchor = new Anchor { Horizontal = hAlign, Vertical = vAlign };
                    GridVector2 origin = ((spacing) * new GridVector2(x, y));// - half_box_size;
                    GridRectangle bbox = new GridRectangle(origin, box_size.X, box_size.Y);

                    string label_text = UseNewlines ? string.Format("{0}\n{1}\nGoldfish", vAlign, hAlign) : string.Format("{0} {1}", vAlign, hAlign);
                    LabeledRectangleView new_view;

                    if (PassRectanglesToConstructor)
                    {
                        new_view = new LabeledRectangleView(label_text, bbox, Color.Black, Color.Black.Random().SetAlpha(0.5f), alignment: new Alignment { Horizontal = hAlign, Vertical = vAlign }, fontSize: 10);
                    }
                    else
                    {
                        new_view = new LabeledRectangleView(label_text, origin, Color.Black, Color.Black.Random().SetAlpha(0.5f), anchor: anchor, alignment: new Alignment { Horizontal = hAlign, Vertical = vAlign }, fontSize: 0.05);
                    }
                    
                    fixed_views.Add(new_view);
                }
            }
        }

        /// <summary>
        /// Places a rectangle so we can be sure RectangleView is drawing as expected
        /// </summary>
        private void LayoutRectangle()
        {
            rect_views = new List<RectangleView>();

            rect_views.Add(new RectangleView(new GridRectangle(-140, -120, -140, -120), Color.Aqua));
        }

        private KeyboardState old_keyboard_state; 

        public void Update()
        {  
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            CameraManipulator.Update(scene.Camera);

            KeyboardState kstate = Keyboard.GetState();
            Keypad.Update(kstate); 

            if (kstate.IsKeyDown(Keys.Space))
            {
                LayoutButtons();
                LayoutRectangle();
            }

            if (old_keyboard_state != null)
            {
                if (Keypad.Pressed(Keys.N))
                {
                    this.UseNewlines = !UseNewlines;
                    LayoutButtons();
                }

                if (Keypad.Pressed(Keys.F))
                {
                    LayoutFixedButtons();
                }

                if (Keypad.Pressed(Keys.R))
                {
                    this.PassRectanglesToConstructor = !PassRectanglesToConstructor;
                    LayoutButtons();
                }

                if(Keypad.Pressed(Keys.OemPlus))
                {
                    scene.Camera.Downsample -= 0.1; 
                }

                if (Keypad.Pressed(Keys.OemMinus))
                {
                    scene.Camera.Downsample += 0.1;
                }
                
            }

            old_keyboard_state = kstate;
        }

        public void Draw(MonoTestbed window)
        {
            LabeledRectangleView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha, views.ToArray());
            RectangleView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha, rect_views.ToArray());
            CircleView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha, dot_views.ToArray());
            //LabeledRectangleView.Draw(window.GraphicsDevice, screen_space_scene, OverlayStyle.Alpha, fixed_views.ToArray());
        }

        public void UnloadContent(MonoTestbed window)
        {
        }
    }
}
