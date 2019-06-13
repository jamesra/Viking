using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    class PenInputHelper
    {
        private double lastAngle;
        private float PointIntervalOnDrag;
        private float PenAngleThreshold;
        public GridVector2 cursor_position;

        public PenInputHelper()
        {
            lastAngle = 0;
            PointIntervalOnDrag = 90;
            PenAngleThreshold = .36f;
            cursor_position = new GridVector2(); 
        }


        
        //Returns true if 
        public int GetNextVertex(MouseEventArgs e, Viking.UI.Controls.SectionViewerControl Parent, GridVector2[] Verticies)
        {
            cursor_position = Parent.ScreenToWorld(e.X, e.Y);
            double distanceToLast = GridVector2.Distance(cursor_position, Verticies.Last());
            double distanceToFirst = GridVector2.Distance(cursor_position, Verticies[0]);

            //Creates a new verticie when the mouse moves set distance away
            if (distanceToLast > this.PointIntervalOnDrag)
            {
                double angle;

                //Measure the slope between the two most recent vertices
                if (Verticies.Length >= 3)
                {
                    angle = GridVector2.ArcAngle(Verticies.Last(), cursor_position, Verticies[Verticies.Length - 2]);


                    if (lastAngle - angle <= PenAngleThreshold && lastAngle - angle >= -PenAngleThreshold)
                    {
                        //Remove the last vertex
                        return -1;
                        //PopVertex();
                    }

                    //Set new slope to be between this point and the NEW last vertice (should be the one that came before the one we just removed)
                    lastAngle = GridVector2.ArcAngle(Verticies.Last(), cursor_position, Verticies[Verticies.Length - 2]);
                }

                return 1;
                //this.PushVertex(cursor_position);
            }
            return 0;
        }

        public void Clear()
        {
            lastAngle = 0;
        }
        
    }

}
