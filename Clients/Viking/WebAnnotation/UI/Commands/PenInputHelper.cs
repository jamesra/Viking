using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VikingXNAWinForms;
using System.Collections.Specialized;

namespace WebAnnotation.UI.Commands
{
    enum VERTEXACTION
    {
        REMOVE,
        REPLACE,
        NONE,
        ADD
    };

    class PenInputHelper
    {
        private double lastAngle;
        private float PointIntervalOnDrag;
        private float PenAngleThreshold;
        Viking.UI.Controls.SectionViewerControl Parent;
        // GridVector2[] Verticies;

        public bool CanPathSelfIntersect = false;

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnPathChanged;

        public List<GridVector2> Path = new List<GridVector2>();

        public PenInputHelper(Viking.UI.Controls.SectionViewerControl Parent)
        {
            lastAngle = 0;
            PointIntervalOnDrag = 90;
            PenAngleThreshold = .36f;
            this.Parent = Parent;

            Parent.MouseMove += this.OnMouseMove;
        }

        private bool CanControlPointBePlaced(GridVector2 position)
        {
            return true;
        }

        public void Push(GridVector2 p)
        {
            this.Path.Insert(0, p);
        }

        public GridVector2 Pop()
        {
            GridVector2 p = this.Path.First();
            this.Path.RemoveAt(0);
            return p;
        }

        public GridVector2 Peek()
        {
            return this.Path.First();
        }

        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button.Left())
            {
                GridVector2 cursor_position = Parent.ScreenToWorld(e.X, e.Y);
                VERTEXACTION placeVertex = this.GetNextVertex(cursor_position);
                if (CanControlPointBePlaced(cursor_position))
                {
                    if (placeVertex == VERTEXACTION.REPLACE)
                    {
                        GridVector2 oldValue = this.Pop();
                        this.Push(cursor_position);
                        OnPathChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, cursor_position, oldValue, 0));
                    }
                    else if (placeVertex == VERTEXACTION.REMOVE)
                    {
                        GridVector2 removed = this.Pop();
                        OnPathChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, 0));
                    }
                    else if (placeVertex == VERTEXACTION.ADD)
                    {
                        this.Push(cursor_position);
                        OnPathChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, cursor_position, 0));
                    }
                }
            }
        }

         
        //Returns true if 
        public VERTEXACTION GetNextVertex(GridVector2 cursor_position)
        {
            if (Path.Count == 0)
                return VERTEXACTION.ADD;

            double distanceToLast = GridVector2.Distance(cursor_position, this.Path.Last());

            //Creates a new verticie when the mouse moves set distance away
            if (distanceToLast > this.PointIntervalOnDrag)
            {
                double angle;

                //Measure the slope between the two most recent vertices
                if (Path.Count >= 3)
                {
                    angle = GridVector2.ArcAngle(Path.Last(), cursor_position, Path[Path.Count - 2]);


                    if (lastAngle - angle <= PenAngleThreshold && lastAngle - angle >= -PenAngleThreshold)
                    {
                        //Remove the last vertex
                        return VERTEXACTION.REPLACE;
                        
                    }

                    //Set new slope to be between this point and the NEW last vertice (should be the one that came before the one we just removed)
                    lastAngle = GridVector2.ArcAngle(Path.Last(), cursor_position, Path[Path.Count - 2]);
                }

                return VERTEXACTION.ADD;
                //this.PushVertex(cursor_position);
            }
            return VERTEXACTION.NONE;
        }
         
        public void Clear()
        {
            lastAngle = 0;
        }
        
    }

}
