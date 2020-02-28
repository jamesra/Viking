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
    [Flags]
    public enum PointLabelType
    {
        NONE = 0x0,
        INDEX = 0x01, //The index of the point in the collection
        POSITION = 0x02 //The position of the point
    }

    /// <summary>
    /// Draw a collection of points, optionally labeling by position, index, or both
    /// </summary>
    class PointSetView : PointViewBase
    { 
        public CircleView[] PointViews = new CircleView[0];
        public LabelView[] LabelViews = new LabelView[0]; 
        private double _PointRadius = 1.0;

        private PointLabelType _LabelType = PointLabelType.NONE;
        public PointLabelType LabelType
        {
            get { return _LabelType; }
            set {
                _LabelType = value;
                UpdateViews();
            }
        }
         
        public bool LabelIndex
        {
            get
            {
                return (_LabelType & PointLabelType.INDEX) > 0;
            }
            set
            {
                if (value)
                {
                    LabelType = _LabelType | PointLabelType.INDEX;
                }
                else
                {
                    LabelType = _LabelType & ~PointLabelType.INDEX;
                }
                 
            }
        }
          
        public bool LabelPosition
        {
            get
            {
                return (_LabelType & PointLabelType.POSITION) > 0;
            }
            set
            {
                if (value)
                {
                    LabelType = _LabelType | PointLabelType.POSITION;
                }
                else
                {
                    LabelType = _LabelType & ~PointLabelType.POSITION;
                }

            }
        }

        private Color _LabelColor = Color.Black;

        public Color LabelColor
        {
            get
            {
                return _LabelColor;
            }

            set
            {
                _LabelColor = value;
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

        public PointSetView(double defaultRadius = 1.0) : this(Color.Gold, defaultRadius)
        {
        }

        public PointSetView(Color defaultColor, double defaultRadius=1.0)
        {
            base.Color = defaultColor;
            _PointRadius = defaultRadius;
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
                    label.Color = this.LabelColor;
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
            PointSetView psv = new PointSetView(Color.Gray);

            psv.LabelColor = Color.YellowGreen;
            psv.PointRadius = 0.5;
            psv.Points = mesh.Verticies.Select(p => p.Position.XY()).ToArray();
            psv.LabelIndex = true;
            psv.LabelPosition = false;
            psv.UpdateViews();

            return psv;
        }
    }
}
