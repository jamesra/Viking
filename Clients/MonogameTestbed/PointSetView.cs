using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using VikingXNAGraphics;
using Microsoft.Xna.Framework;
using VikingXNA;

namespace MonogameTestbed
{

    class PointSetView : PointViewBase
    { 
        public CircleView[] PointViews = new CircleView[0];
        public LabelView[] LabelViews = new LabelView[0]; 
        private double _PointRadius = 1.0;

        private bool _LabelIndex = false;
        public bool LabelIndex
        {
            get
            {
                return _LabelIndex;
            }
            set
            {
                _LabelIndex = value;
                UpdateViews();
            }
        }

        private bool _LabelPosition = true;

        public bool LabelPosition
        {
            get
            {
                return _LabelPosition;
            }
            set
            {
                _LabelPosition = value;
                UpdateViews();
            }
        }


        public double PointRadius
        {
            get { return _PointRadius; }
            set
            {
                _PointRadius = value;
                UpdateViews();
            }
        }

        public override void UpdateViews()
        {
            if (Points == null)
            {
                PointViews = new CircleView[0];
                LabelViews = new LabelView[0];
                return;
            }

            PointViews = Points.Select(p => new CircleView(new GridCircle(p, PointRadius), Color)).ToArray();

            if (!LabelIndex && !LabelPosition)
            {
                LabelViews = null;
            }
            else if (LabelIndex && !LabelPosition)
            {
                LabelViews = Points.Select((p, i) => new LabelView(i.ToString(), p, fontSize: this.PointRadius * 2)).ToArray();
            }
            else if (!LabelIndex && LabelPosition)
            {
                LabelViews = Points.Select(p => new LabelView(p.ToLabel(), p, fontSize: this.PointRadius * 2)).ToArray();
            }
            else
            {
                LabelViews = Points.Select((p, i) => new LabelView(i.ToString() + "\n" + p.ToLabel(), p, fontSize: this.PointRadius * 2)).ToArray();
            }

            if (LabelViews != null)
            {
                foreach (LabelView label in LabelViews)
                {
                    label.FontSize = this.PointRadius * 2.0;
                }
            }
        }

        public override void Draw(MonoTestbed window, Scene scene)
        { 
            if (PointViews != null)
                CircleView.Draw(window.GraphicsDevice, scene, window.basicEffect, window.overlayEffect, PointViews);

            if (LabelViews != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, LabelViews);
        }

        public static PointSetView CreateFor(MorphologyMesh.MorphRenderMesh mesh)
        {    
            PointSetView psv = new PointSetView();

            psv.PointRadius = 0.5;
            psv.Points = mesh.Verticies.Select(p => p.Position.XY()).ToArray();
            psv.LabelIndex = true;
            psv.LabelPosition = false;
            psv.UpdateViews();

            return psv;
        }
    }
}
