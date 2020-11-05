using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Geometry
{
    internal enum Quadrant
    {
        UPPERLEFT = 0,
        UPPERRIGHT = 1,
        LOWERLEFT = 2,
        LOWERRIGHT = 3
    };

    internal static class QuadrantExtensions
    {
        public static Quadrant Opposite(this Quadrant quad)
        {
            switch (quad)
            {
                case Quadrant.LOWERLEFT:
                    return Quadrant.UPPERRIGHT;
                case Quadrant.LOWERRIGHT:
                    return Quadrant.UPPERLEFT;
                case Quadrant.UPPERLEFT:
                    return Quadrant.LOWERRIGHT;
                case Quadrant.UPPERRIGHT:
                    return Quadrant.LOWERLEFT;
                default:
                    throw new ArgumentException("Unexpected quadrant");
            }

        }
    }


    internal class QuadTreeNode<T>
    {
        internal QuadTree<T> Tree;
        internal QuadTreeNode<T> Parent = null;

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        private readonly QuadTreeNode<T>[] _quadrants = new QuadTreeNode<T>[] { null, null, null, null };

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNode<T> UpperLeft
        {
            get { return _quadrants[(int)Quadrant.UPPERLEFT]; }
        }

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNode<T> UpperRight
        {
            get { return _quadrants[(int)Quadrant.UPPERRIGHT]; }
        }

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        /// 
        QuadTreeNode<T> LowerLeft
        {
            get { return _quadrants[(int)Quadrant.LOWERLEFT]; }
        }

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNode<T> LowerRight
        {
            get { return _quadrants[(int)Quadrant.LOWERRIGHT]; }
        }

        public QuadTreeNode<T> this[Quadrant quad]
        {
            get
            {
                return _quadrants[(int)quad];
            }
            set
            {
                _quadrants[(int)quad] = value;
            }
        }

        /// <summary>
        /// Returns the number of non-null children
        /// </summary>
        int NumChildren
        {
            get
            {
                int count = _quadrants.Count(q => q != null);
                return count;
            }
        }

        private GridRectangle? _Border;

        internal bool HasBorder { get { return _Border.HasValue; } }

        internal GridRectangle Border
        {
            get { return _Border.Value; }
            set
            {
                if (_Border.HasValue)
                {
                    throw new ArgumentException("Should not set the Border property more than once.");
                }

                _Border = new GridRectangle?(value);
            }
        }

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
        public GridVector2 Point = new GridVector2(double.MinValue, double.MinValue);

        /// <summary>
        /// Set to true if the value field is valid
        /// </summary>
        public bool HasValue = false;

        /// <summary>
        /// The data held by this node
        /// </summary>
        public T Value;

        public bool IsLeaf
        {
            get
            {
                return UpperLeft == null && UpperRight == null &&
                        LowerLeft == null && LowerRight == null;
            }
        }

        public bool IsRoot
        {
            get
            {
                return Parent == null;
            }
        }

        /// <summary>
        /// This constructor is used to create the root node
        /// </summary>
        /// <param name="border"></param>
        public QuadTreeNode(QuadTree<T> tree)
        {
            this.Tree = tree;
        }

        /// <summary>
        /// This constructor is used to create the root node
        /// </summary>
        /// <param name="border"></param>
        public QuadTreeNode(QuadTree<T> tree, GridRectangle border)
        {
            this.Tree = tree;
            this.Border = border;
        }

        public QuadTreeNode(QuadTreeNode<T> Parent, Quadrant quad)
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

        public QuadTreeNode(QuadTreeNode<T> Parent, Quadrant quad, GridVector2 point, T value)
            : this(Parent, quad)
        {
            this.Point = point;
            this.Value = value;
            this.HasValue = true;

            Parent.Tree.ValueToNodeTable.Add(value, this);

            Debug.Assert(this.Border.Contains(point));
        }

        /// <summary>
        /// Given a point returns the quadrant the point should be in
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private Quadrant GetQuad(GridVector2 point)
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
        public QuadTreeNode<T> Insert(GridVector2 point, T value)
        {
            Debug.Assert((HasBorder && Border.Contains(point)) || (this.IsRoot && this.HasValue == false), "QuadNode must contain point for insert to succeed");
            Debug.Assert((HasBorder && HasValue && Border.Contains(Point)) || !IsLeaf || (this.IsRoot && this.HasValue == false), "QuadNode must contain its own point for insert to succeed");

            //If we are a leaf node, we need to divide and create new leaf nodes
            if (this.IsLeaf)
            {
                //Check for the default point value in case this is the root of the tree
                if (this.IsRoot && this.HasValue == false)
                {
                    this.Point = point;
                    this.Value = value;
                    this.HasValue = true;
                    Tree.ValueToNodeTable.Add(this.Value, this);
                    return this;
                }
                //Check that the point we are being asked to insert is not a duplicate of our current point
                else if (this.Point == point)
                {
                    throw new QuadTree<T>.DuplicateItemException(point);
                    //return null;
                }
                else // It is a new point.  We need to create children for this node and insert the points
                {
                    //First create a child for the existing point

                    //Remove ourselves from the table, must be done before constructor
                    Tree.ValueToNodeTable.Remove(this.Value);

                    Quadrant quad = GetQuad(this.Point);

                    _quadrants[(int)quad] = new QuadTreeNode<T>(this, quad, this.Point, this.Value);

                    //Erase our point just to be safe since we aren't a leaf anymore
                    this.Point = new GridVector2();
                    this.Value = default(T);
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
                    _quadrants[(int)quad] = new QuadTreeNode<T>(this, quad, point, value);
                    return _quadrants[(int)quad];
                }
                else
                {
                    //If we have created a node for that quadrant then recursively call insert
                    return _quadrants[(int)quad].Insert(point, value);
                }
            }
        }


        private static double RoundUpToNearestPowerOfTen(double val)
        {
            Debug.Assert(val > 0);
            return Math.Pow(10, Math.Ceiling(Math.Log10(val)));
        }
        /// <summary>
        /// Insert nodes at the root to expand our borders large enough to include the point
        /// </summary>
        /// <param name="point"></param>
        /// <returns>The new root node if the border expanded or was defined</returns>

        internal bool ExpandBorder(GridVector2 point, out QuadTreeNode<T> new_root)
        {
            new_root = null;
            if (HasBorder && Border.Contains(point))
                return false;

            if (this.HasBorder == false)
            {
                Debug.Assert(this.IsRoot, "The only reason a node should not have a border is if it is the root node and no bounds were set at construction");
                if (this.IsLeaf && this.HasValue == false)
                {
                    //If this is the first point, then don't worry about the bounds
                    return false;
                }
                else if (this.HasValue)
                {
                    //If this is the 2nd point and we have no border, estimate the border from the two points
                    GridVector2 BoxDistance = this.Point - point;
                    //Create a boundary centered on our root node that will cover the 2nd point
                    double quad_size = Math.Max(Math.Abs(BoxDistance.X * 2), Math.Abs(BoxDistance.Y * 2));
                    double rounded_quad_size = RoundUpToNearestPowerOfTen(quad_size);
                    GridRectangle Bounds = new GridRectangle(point, rounded_quad_size);
                    this.Border = Bounds;

                    Debug.Assert(Bounds.Contains(Point), "The border specified must include the node's point");
                    Debug.Assert(Bounds.Contains(point), "The border specified must include the new point");
                    if (Bounds.Contains(Point) == false)
                    {
                        throw new ArgumentException("The border specified must include the node's point");
                    }
                    if (Bounds.Contains(point) == false)
                    {
                        throw new ArgumentException("The border specified must include the new point");
                    }

                    new_root = this;

                    //Trace.WriteLine(string.Format("Calculated border of {0}", Bounds));
                    return true;
                }

                throw new ArgumentException("Unexpected code path reached in QuadTree ExpandBorder");
            }

            GridRectangle parent_bounds;

            double ParentWidth = this.Border.Width * 2;
            double ParentHeight = this.Border.Height * 2;

            GridVector2 ParentCenter;

            Quadrant quad = GetQuad(point);

            switch (quad)
            {
                case Quadrant.LOWERLEFT:
                    //We are the upper-right node of the new root.
                    ParentCenter = Border.LowerLeft;
                    break;
                case Quadrant.LOWERRIGHT:
                    //We are the Upper-left node of the new root.
                    ParentCenter = Border.LowerRight;
                    break;
                case Quadrant.UPPERLEFT:
                    //We are the Lower-Right node of the new root
                    ParentCenter = Border.UpperLeft;
                    break;
                case Quadrant.UPPERRIGHT:
                    //We are the Lower-Left node of the new root
                    ParentCenter = Border.UpperRight;
                    break;
                default:
                    throw new ArgumentException("Unexpected quadrant");
            }

            parent_bounds = new GridRectangle(ParentCenter, this.Border.Width);//- new GridVector2(Border.Width, Border.Height), ParentWidth, ParentHeight);

            QuadTreeNode<T> new_parent = new QuadTreeNode<T>(this.Tree, parent_bounds);
            new_parent[quad.Opposite()] = this;
            this.Parent = new_parent;

            //Trace.WriteLine(string.Format("Expanded border from {0} to {1}", this.Border, parent_bounds));
            /*
            Debug.Assert(parent_bounds.Contains(this.Center), "New root node must include center of this quad");
            if(parent_bounds.Contains(this.Center) == false)
            {
                throw new ArgumentException("New root node must include center of this quad");
            }

           */

            if (new_parent.ExpandBorder(point, out new_root))
            {
                Debug.Assert((this.IsLeaf == false) || new_root.Border.Contains(Point), "New root node must include our point");
                Debug.Assert(new_root.Border.Contains(point), "New root node must include new point");
                return true;
            }
            else
            {
                new_root = new_parent;
                Debug.Assert((this.IsLeaf == false) || new_root.Border.Contains(Point), "New root node must include our point");
                Debug.Assert(new_root.Border.Contains(point), "New root node must include new point");
                return true;
            }
        }


        /// <summary>
        /// Delete the node from the subtree of this node.  Should be an immediate child of this node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public void Remove(QuadTreeNode<T> node)
        {
            if (node.HasValue)
                Tree.ValueToNodeTable.Remove(node.Value);

            node.Value = default(T);
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
                if (IsRoot == false)
                {
                    Parent.Remove(this);
                }
                else
                {
                    //Looks like we are the last node in the tree
                    //Tree.ValueToNodeTable.Remove(this.Value);
                    //this.Value = default(T);
                    //this.HasValue = false;
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
        public T FindNearest(GridVector2 point, out GridVector2 nodePoint, ref double distance)
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
                T retValue = default(T);
                nodePoint = new GridVector2(double.MinValue, double.MinValue);

                //If we aren't a leaf node then do a depth first search to find the nearest point
                if (_quadrants[(int)quad] != null)
                {
                    retValue = _quadrants[(int)quad].FindNearest(point, out nodePoint, ref distance);
                }

                if (distance == 0)
                {
                    //Can't get any closer than 0.  Stop looking
                    return retValue;
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
                        if (_quadrants[iQuad].Border.Intersects(rect))
                        {
                            double newDistance = double.MaxValue;
                            GridVector2 foundNode;
                            T foundValue = _quadrants[iQuad].FindNearest(point, out foundNode, ref newDistance);

                            if (newDistance < distance)
                            {
                                nodePoint = foundNode;
                                retValue = foundValue;
                                distance = newDistance;
                                if (newDistance == 0)
                                {
                                    //Can't get any closer than 0.  Stop looking
                                    return retValue;
                                }

                                rect = new GridRectangle(point, distance);
                            }


                        }
                    }
                }

                //OK, we have the best value we can
                return retValue;
            }
        }

        /// <summary>
        /// Returns the value associated with the point nearest to the passed input parameter point
        /// </summary>
        /// <param name="point">Query point</param>
        /// <param name="nodePoint">Nearest point in QuadTree to query point</param>
        /// <param name="distance">Distance from query point to nodePoint</param>
        /// <returns>Data value associated with nearest point</returns>
        public bool FindNearestPoints(GridVector2 point, int nPoints, ref FixedSizeDistanceList<T> distanceList)
        {
            if (nPoints == 0)
            {
                return false;
            }

            if (this.IsLeaf)
            {
                Debug.Assert(this.HasValue);
                double distance = GridVector2.Distance(this.Point, point);

                return distanceList.TryAdd(new DistanceToPoint<T>(this.Point, distance, Value));
            }
            else
            {
                //Set to true if any child added a point
                bool PointFound = false;

                Quadrant quad = GetQuad(point);
                GridVector2 nodePoint = new GridVector2(double.MinValue, double.MinValue);

                //If we aren't a leaf node then do a depth first search to find the nearest point
                if (_quadrants[(int)quad] != null)
                {
                    PointFound = _quadrants[(int)quad].FindNearestPoints(point, nPoints, ref distanceList);
                }

                //Next we check our other quadrants to see if it is possible they could have a closer point
                //It is OK if we didn't have a quadrant for the point in the earlier check because then the default values for 
                //distance force the adjacent quadrants to be checked

                double maxDistance = double.MaxValue;

                //If we've already located enough points to fill our list, then only search for points that may be closer than points in the list
                if (distanceList.Count >= nPoints)
                    maxDistance = distanceList.MaxDistance;

                GridRectangle rect = new GridRectangle(point, maxDistance);

                //If we aren't a leaf, then check each of our children for the nearest point
                for (int iQuad = 0; iQuad < 4; iQuad++)
                {
                    //Don't double check the quadrant we checked earlier
                    if (iQuad == (int)quad)
                        continue;

                    if (_quadrants[iQuad] != null)
                    {
                        //If it is possible the neighboring quadrant has a closer point then check it and update if a nearer point is found
                        if (_quadrants[iQuad].Border.Intersects(rect) || distanceList.Count < nPoints)
                        {
                            bool ListChanged = _quadrants[iQuad].FindNearestPoints(point, nPoints, ref distanceList);

                            if (ListChanged)
                            {
                                PointFound = true;

                                maxDistance = distanceList.MaxDistance;
                                //Determine the furthest point we have found and set the bounding rectangle of what we need to check accordingly
                                rect = new GridRectangle(point, maxDistance);
                            }
                        }
                    }
                }

                //OK, we have the best value we can
                return PointFound;
            }
        }


        //Returns a list of all points inside the specified rectangle.  If test is false a parents test determined the border
        //was completely inside the RequestRect and no further testing was needed
        public void Intersect(GridRectangle RequestRect,
                                            bool NeedTest,
                                            ref List<GridVector2> Points,
                                            ref List<T> Values)
        {
            if (this.IsLeaf)
            {
                if (this.HasValue == false)
                    return;


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
                if (NeedTest)
                {

                    if (Border.Intersects(RequestRect))
                    {

                        if (RequestRect.Contains(Border))
                        {
                            if (this.UpperLeft != null)
                            {
                                this.UpperLeft.Intersect(RequestRect, false, ref Points, ref Values);
                            }
                            if (this.UpperRight != null)
                            {
                                this.UpperRight.Intersect(RequestRect, false, ref Points, ref Values);
                            }
                            if (this.LowerLeft != null)
                            {
                                this.LowerLeft.Intersect(RequestRect, false, ref Points, ref Values);
                            }
                            if (this.LowerRight != null)
                            {
                                this.LowerRight.Intersect(RequestRect, false, ref Points, ref Values);
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
                    this.UpperLeft.Intersect(RequestRect, true, ref Points, ref Values);
                }
                if (this.UpperRight != null)
                {
                    this.UpperRight.Intersect(RequestRect, true, ref Points, ref Values);
                }
                if (this.LowerLeft != null)
                {
                    this.LowerLeft.Intersect(RequestRect, true, ref Points, ref Values);
                }
                if (this.LowerRight != null)
                {
                    this.LowerRight.Intersect(RequestRect, true, ref Points, ref Values);
                }
                return;

            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (this.IsRoot)
                sb.Append("Root: ");
            if (this.IsLeaf)
                sb.Append("Leaf: ");
            else
                sb.Append("Branch:");

            if (this.HasValue)
            {
                sb.Append(this.Point);
            }
            else if (this.HasBorder)
            {
                sb.Append(this.Border);
            }

            return sb.ToString();
        }
    }
}
