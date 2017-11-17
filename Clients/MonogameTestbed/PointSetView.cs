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
        private double _PointRadius = 2.0;

        public bool LabelIndex = false; 

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
            if(Points == null)
            {
                PointViews = new CircleView[0];
                LabelViews = new LabelView[0];
                return; 
            }

            PointViews = Points.Select(p => new CircleView( new GridCircle(p, PointRadius), Color)).ToArray();

            if(!LabelIndex)
                LabelViews = Points.Select(p => new LabelView(p.ToLabel(), p)).ToArray();
            else
                LabelViews = Points.Select((p,i) => new LabelView(i.ToString() + " " + p.ToLabel(), p)).ToArray();

            foreach (LabelView label in LabelViews)
            {
                label.FontSize = _PointRadius;
            }
        }

        public override void Draw(MonoTestbed window, Scene scene)
        { 
            if (PointViews != null)
                CircleView.Draw(window.GraphicsDevice, scene, window.basicEffect, window.overlayEffect, PointViews);

            if (LabelViews != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, LabelViews);
        }
    }
}
