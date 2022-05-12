using System.Collections.Generic;
using System.Diagnostics;

namespace Geometry
{
    internal class QuadTreeNodeTemplatePoint<TPoint, TValue>
        where TPoint : struct, IPoint2D
    {
        readonly QuadTreeTemplatePoint<TPoint, TValue> Tree;
        internal QuadTreeNodeTemplatePoint<TPoint, TValue> Parent = null;

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        public enum Quadrant
        {
            UPPERLEFT = 0,
            UPPERRIGHT = 1,
            LOWERLEFT = 2,
            LOWERRIGHT = 3
        };

        private readonly QuadTreeNodeTemplatePoint<TPoint, TValue>[] _quadrants = new QuadTreeNodeTemplatePoint<TPoint, TValue>[] { null, null, null, null };

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNodeTemplatePoint<TPoint, TValue> UpperLeft
        {
            get { return _quadrants[(int)Quadrant.UPPERLEFT]; }
        }

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNodeTemplatePoint<TPoint, TValue> UpperRight
        {
            get { return _quadrants[(int)Quadrant.UPPERRIGHT]; }
        }

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        /// 
        QuadTreeNodeTemplatePoint<TPoint, TValue> LowerLeft
        {
            get { return _quadrants[(int)Quadrant.LOWERLEFT]; }
        }

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNodeTemplatePoint<TPoint, TValue> LowerRight
        {
            get { return _quadrants[(int)Quadrant.LOWERRIGHT]; }
        }

        /// <summary>
        /// Returns the number of non-null children
        /// </summary>
        int NumChildren
        {
            get
            {
                int count = 0;
                if (_quadrants[(int)Quadrant.UPPERLEFT] != null)
                    count++;
                if (_quadrants[(int)Quadrant.UPPERRIGHT] != null)
                    count++;
                if (_quadrants[(int)Quadrant.LOWERLEFT] != null)
                    count++;
                if (_quadrants[(int)Quadrant.LOWERRIGHT] != null)
                    count++;

                return count;
            }
        }

        internal GridRectangle Border;
        protected GridVector2 Center
        {
            get
            {
                return Border.Center;
            }
        }

        /// <summary>
        /// If this node is a leaf then Point contains the position of the point in this node
        /// </summary>
        public TPoint Point;

        /// <summary>
        /// Set to true if the value field is valid
        /// </summary>
        public bool HasValue = false;

        /// <summary>
        /// The data held by this node
        /// </summary>
        public TValue Value;

        public bool IsLeaf
        {
            get
            {
                return UpperLeft == null && UpperRight == null &&
                        LowerLeft == null && LowerRight == null;
            }
        }

        /// <summary>
        /// This constructor is used to create the root node
        /// </summary>
        /// <param name="border"></param>
        public QuadTreeNodeTemplatePoint(QuadTreeTemplatePoint<TPoint, TValue> tree, GridRectangle border)
        {
            this.Tree = tree;
            this.Border = border;
            this.Point.X = double.MinValue;
            this.Point.Y = double.MinValue;

            Debug.Assert(this.Border.Width > 0 && this.Border.Height > 0);
        }

        public QuadTreeNodeTemplatePoint(QuadTreeNodeTemplatePoint<TPoint, TValue> Parent, Quadrant quad)
        {
            //Figure out our new boundaries
            this.Parent = Parent;

            switch (quad)
            {
                case Quadrant.UPPERLEFT:
                    this.Border = new GridRectangle(Parent.Border.Left, Parent.Border.Center.X, Parent.Border.Center.Y, Parent.Border.Top);
                    break;
                case Quadrant.UPPERRIGHT:
                    this.Border = new GridRectangle(Parent.Border.Center.X, Parent.Border.Right, Parent.Border.Center.Y, Parent.Border.Top);
                    break;
                case Quadrant.LOWERLEFT:
                    this.Border = new GridRectangle(Parent.Border.Left, Parent.Border.Center.X, Parent.Border.Bottom, Parent.Border.Center.Y);
                    break;
                case Quadrant.LOWERRIGHT:
                    this.Border = new GridRectangle(Parent.Border.Center.X, Parent.Border.Right, Parent.Border.Bottom, Parent.Border.Center.Y);
                    break;
            }

            this.Tree = Parent.Tree;
        }

        public QuadTreeNodeTemplatePoint(QuadTreeNodeTemplatePoint<TPoint, TValue> Parent, Quadrant quad, TPoint point, TValue value)
            : this(Parent, quad)
        {
            this.Point = point;
            this.Value = value;
            this.HasValue = true;

            Parent.Tree.ValueToNodeTable.Add(value, this);
        }

        /// <summary>
        /// Given a point returns the quadrant the point should be in
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private Quadrant GetQuad(IPoint2D point)
        {
            Quadrant quad;

            GridVector2 center = this.Center;

            if (point.X > center.X) //Right of center
            {

                if (point.Y > center.Y)
                {
                    quad = Quadrant.UPPERRIGHT;
                }
                else
                {
                    quad = Quadrant.LOWERRIGHT;
                }
            }
            else //Left of center
            {
                if (point.Y > center.Y)
                {
                    quad = Quadrant.UPPERLEFT;
                }
                else
                {
                    quad = Quadrant.LOWERLEFT;
                }
            }

            return quad;
        }


        /// <summary>
        /// Inserts a point into the tree.  Returns the new QuadTreeNode the caller should point to as the root of the tree
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public QuadTreeNodeTemplatePoint<TPoint, TValue> Insert(TPoint point, TValue value)
        {
            //If we are a leaf node, we need to divide and create new leaf nodes
            if (this.IsLeaf)
            {
                //Check for the default point value in case this is the root of the tree
                if (this.Parent == null && this.HasValue == false)
                {
                    this.Point = point;
                    this.Value = value;
                    this.HasValue = true;
                    Tree.ValueToNodeTable.Add(this.Value, this);
                    return this;
                }
                //Check that the point we are being asked to insert is not a duplicate of our current point
                else if (this.Point.Equals(point))
                {
                    //throw new ArgumentException("The point being inserted into the quad tree is a duplicate point: " + point.ToString(), "point");
                    return null;
                }
                else // It is a new point.  We need to create children for this node and insert the points
                {
                    //First create a child for the existing point

                    //REmove ourselves from the table, must be done before constructor
                    Tree.ValueToNodeTable.Remove(this.Value);

                    Quadrant quad = GetQuad(this.Point);

                    _quadrants[(int)quad] = new QuadTreeNodeTemplatePoint<TPoint, TValue>(this, quad, this.Point, this.Value);

                    //Erase our point just to be safe since we aren't a leaf anymore
                    this.Point = default;
                    this.Value = default;
                    this.HasValue = false;

                    //Call insert on ourselves to insert the new point
                    return this.Insert(point, value);
                }
            }
            //If we are not a leaf node, insert into the appropriate quadrant if it exists
            else
            {
                Quadrant quad = GetQuad(point);

                //If we haven't created a node for this quadrant then do so...
                if (_quadrants[(int)quad] == null)
                {
                    _quadrants[(int)quad] = new QuadTreeNodeTemplatePoint<TPoint, TValue>(this, quad, point, value);
                    return _quadrants[(int)quad];
                }
                else
                {
                    //If we have created a node for that quadrant then recursively call insert
                    return _quadrants[(int)quad].Insert(point, value);
                }
            }
        }

        /// <summary>
        /// Delete the node from the subtree of this node.  Should be an immediate child of this node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public void Remove(QuadTreeNodeTemplatePoint<TPoint, TValue> node)
        {
            if (node.HasValue)
                Tree.ValueToNodeTable.Remove(node.Value);

            node.Value = default;
            node.HasValue = false;

            //Figure out which quadrant the node lives in
            Quadrant quad = GetQuad(node.Center);

            //Remove the node from our list
            this._quadrants[(int)quad] = null;

            if (this.NumChildren > 0)
                return;
            else if (this.NumChildren == 0)
            {
                //In this case the node has no remaining children so we should remove ourselves from our parent
                if (Parent != null)
                {
                    Parent.Remove(this);
                }
                else
                {
                    //Looks like we are the last node in the tree
                    Tree.ValueToNodeTable.Remove(this.Value);
                    this.Value = default;
                    this.HasValue = false;
                }
            }
        }

        /// <summary>
        /// Returns the value associated with the point nearest to the passed input parameter point
        /// </summary>
        /// <param name="point">Query point</param>
        /// <param name="nodePoint">Nearest point in QuadTree to query point</param>
        /// <param name="distance">Distance from query point to nodePoint</param>
        /// <returns>Data value associated with nearest point</returns>
        public TValue FindNearest(TPoint point, out TPoint nodePoint, ref double distance)
        {
            if (this.IsLeaf)
            {
                Debug.Assert(this.HasValue);
                distance = GridVector2.Distance(this.Point, point);
                nodePoint = this.Point;
                return this.Value;
            }
            else
            {
                Quadrant quad = GetQuad(point);
                TValue retValue = default;
                nodePoint = default;
                nodePoint.X = double.MinValue;
                nodePoint.Y = double.MinValue;

                //If we aren't a leaf node then do a depth first search to find the nearest point
                if (_quadrants[(int)quad] != null)
                {
                    retValue = _quadrants[(int)quad].FindNearest(point, out nodePoint, ref distance);
                }

                //Next we check our other quadrants to see if it is possible they could have a closer point
                //It is OK if we didn't have a quadrant for the point in the earlier check because then the default values for 
                //distance force the adjacent quadrants to be checked

                GridRectangle rect = new GridRectangle(point, distance);

                //If we aren't a leaf, then check each of our children for the nearest point
                for (int iQuad = 0; iQuad < 4; iQuad++)
                {
                    //Don't double check the quadrant we checked earlier
                    if (iQuad == (int)quad)
                        continue;

                    if (_quadrants[iQuad] != null)
                    {
                        //If it is possible the neighboring quadrant has a closer point then check it and update if a nearer point is found
                        if (_quadrants[iQuad].Border.Intersects(in rect))
                        {
                            double newDistance = double.MaxValue;
                            TValue foundValue = _quadrants[iQuad].FindNearest(point, out TPoint foundNode, ref newDistance);

                            if (newDistance < distance)
                            {
                                nodePoint = foundNode;
                                retValue = foundValue;
                                distance = newDistance;
                                rect = new GridRectangle(point, distance);
                            }
                        }
                    }
                }

                //OK, we have the best value we can
                return retValue;
            }
        }


        //Returns a list of all points inside the specified rectangle.  If test is false a parents test determined the border
        //was completely inside the RequestRect and no further testing was needed
        public void Intersect(in GridRectangle RequestRect,
                                        bool NeedTest,
                                        out List<TPoint> Points,
                                        out List<TValue> Values)
        {
            if (this.IsLeaf)
            {
                Points = new List<TPoint>(1);
                Values = new List<TValue>(1);


                if (NeedTest)
                {
                    if (RequestRect.Contains(this.Point))
                    {
                        Points.Add(this.Point);
                        Values.Add(this.Value);
                    }
                }
                else
                {
                    Points.Add(this.Point);
                    Values.Add(this.Value);
                }

                return;
            }
            else
            {
                Points = new List<TPoint>(4);
                Values = new List<TValue>(4);
                List<TPoint> outPoints;
                List<TValue> outValues;

                if (NeedTest)
                {

                    if (Border.Intersects(RequestRect))
                    {

                        if (RequestRect.Contains(Border))
                        {
                            if (this.UpperLeft != null)
                            {
                                this.UpperLeft.Intersect(in RequestRect, false, out outPoints, out outValues);
                                Points.AddRange(outPoints);
                                Values.AddRange(outValues);
                            }
                            if (this.UpperRight != null)
                            {
                                this.UpperRight.Intersect(in RequestRect, false, out outPoints, out outValues);
                                Points.AddRange(outPoints);
                                Values.AddRange(outValues);
                            }
                            if (this.LowerLeft != null)
                            {
                                this.LowerLeft.Intersect(in RequestRect, false, out outPoints, out outValues);
                                Points.AddRange(outPoints);
                                Values.AddRange(outValues);
                            }
                            if (this.LowerRight != null)
                            {
                                this.LowerRight.Intersect(in RequestRect, false, out outPoints, out outValues);
                                Points.AddRange(outPoints);
                                Values.AddRange(outValues);
                            }

                            return;
                        }
                        //else fall through to calls below requiring test
                    }
                    else
                        //Does not intersect.  Return empty list
                        return;
                }

                if (this.UpperLeft != null)
                {
                    this.UpperLeft.Intersect(in RequestRect, true, out outPoints, out outValues);
                    Points.AddRange(outPoints);
                    Values.AddRange(outValues);
                }
                if (this.UpperRight != null)
                {
                    this.UpperRight.Intersect(in RequestRect, true, out outPoints, out outValues);
                    Points.AddRange(outPoints);
                    Values.AddRange(outValues);
                }
                if (this.LowerLeft != null)
                {
                    this.LowerLeft.Intersect(in RequestRect, true, out outPoints, out outValues);
                    Points.AddRange(outPoints);
                    Values.AddRange(outValues);
                }
                if (this.LowerRight != null)
                {
                    this.LowerRight.Intersect(in RequestRect, true, out outPoints, out outValues);
                    Points.AddRange(outPoints);
                    Values.AddRange(outValues);
                }
                return;

            }
        }
    }
}
