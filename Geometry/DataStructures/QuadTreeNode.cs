using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Geometry
{
    public enum Quadrant : System.Int32
    {
        UpperLeft = 0,
        UpperRight = 1,
        LowerLeft = 2,
        LowerRight = 3
    };

    public static class QuadrantExtensions
    {
        public static Quadrant Opposite(this Quadrant quad)
        {
            return quad switch
            {
                Quadrant.LowerLeft => Quadrant.UpperRight,
                Quadrant.LowerRight => Quadrant.UpperLeft,
                Quadrant.UpperLeft => Quadrant.LowerRight,
                Quadrant.UpperRight => Quadrant.LowerLeft,
                _ => throw new ArgumentException("Unexpected quadrant"),
            };
        }
    }

    public class QuadTreeNode<T>
    {
        readonly QuadTree<T> Tree;
        internal QuadTreeNode<T> Parent = null;

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        private readonly QuadTreeNode<T>[] _quadrants = [null, null, null, null];

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNode<T> UpperLeft => _quadrants[(int)Quadrant.UpperLeft];

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNode<T> UpperRight => _quadrants[(int)Quadrant.UpperRight];

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        /// 
        QuadTreeNode<T> LowerLeft => _quadrants[(int)Quadrant.LowerLeft];

        /// <summary>
        /// It is assumed the "up" has a larger Y value than "down"
        /// </summary>
        QuadTreeNode<T> LowerRight => _quadrants[(int)Quadrant.LowerRight];

        public QuadTreeNode<T> this[Quadrant quad]
        {
            get => _quadrants[(int)quad];
            set => _quadrants[(int)quad] = value;
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

        private Rectangle? _Border;

        internal bool HasBorder => _Border.HasValue;

        internal Rectangle Border
        {
            get => _Border.Value;
            set
            {
                if (_Border.HasValue)
                {
                    throw new ArgumentException("Should not set the Border property more than once.");
                }

                _Border = new Rectangle?(value);
            }
        }

        protected Vector2 Center => Border.Center;

        /// <summary>
        /// If this node is a leaf then Point contains the position of the point in this node
        /// </summary>
        public Vector2 Point = new(double.MinValue, double.MinValue);

        /// <summary>
        /// Set to true if the value field is valid
        /// </summary>
        public bool HasValue = false;

        /// <summary>
        /// The data held by this node
        /// </summary>
        public T Value;

        public bool IsLeaf => _quadrants.All(q => q is null);

        public bool IsRoot => Parent is null;

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
        public QuadTreeNode(QuadTree<T> tree, Rectangle border)
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
                case Quadrant.UpperLeft:
                    this.Border = new Rectangle(Parent.Border.Left, Parent.Border.Center.X, Parent.Border.Center.Y, Parent.Border.Top);
                    break;
                case Quadrant.UpperRight:
                    this.Border = new Rectangle(Parent.Border.Center.X, Parent.Border.Right, Parent.Border.Center.Y, Parent.Border.Top);
                    break;
                case Quadrant.LowerLeft:
                    this.Border = new Rectangle(Parent.Border.Left, Parent.Border.Center.X, Parent.Border.Bottom, Parent.Border.Center.Y);
                    break;
                case Quadrant.LowerRight:
                    this.Border = new Rectangle(Parent.Border.Center.X, Parent.Border.Right, Parent.Border.Bottom, Parent.Border.Center.Y);
                    break;
            }

            this.Tree = Parent.Tree;
        }

        public QuadTreeNode(QuadTreeNode<T> Parent, Quadrant quad, Vector2 point, T value)
            : this(Parent, quad)
        {
            this.Point = point;
            this.Value = value;
            this.HasValue = true;

            Debug.Assert(this.Border.Covers(point));
        }

        /// <summary>
        /// Given a point returns the quadrant the point should be in
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private Quadrant GetQuad(Vector2 point)
        {
            Quadrant quad;


            Vector2 center = this.Center;
            //Debug.Assert(center != point, "We cannot assign a quadrant for a point at the origin");

            quad = point.X > center.X
                ? point.Y > center.Y ? Quadrant.UpperRight : Quadrant.LowerRight
                : point.Y > center.Y ? Quadrant.UpperLeft : Quadrant.LowerLeft;

            return quad;
        }


        public IEnumerable<Vector2> Keys
        {
            get
            {
                if (this.IsLeaf && this.HasValue)
                {
                    yield return this.Point;
                }
                else
                {
                    foreach (var quad in _quadrants.Where(q => q is not null))
                    {
                        foreach (var key in quad.Keys)
                        {
                            yield return key;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Inserts a point into the treeWithUniqueValues.  Returns the new QuadTreeNode the caller should point to as the root of the treeWithUniqueValues
        /// </summary>
        /// <param name="Point"></param>
        /// <param name="output">The node the new point was added to</param>
        /// <returns></returns>
        public QuadTreeNode<T> Insert(Vector2 insertingPoint, T value)
        {
            //Trace.WriteLine($"Insert {insertingPoint} in {this}");
            Debug.Assert((HasBorder && Border.Covers(insertingPoint)) || (this.IsRoot && this.HasValue == false), "QuadNode boundary must contain point for insert to succeed");
            Debug.Assert((HasBorder && HasValue && Border.Covers(Point)) || !IsLeaf || (this.IsRoot && this.HasValue == false), "QuadNode must contain its own point for insert to succeed");

            //If we are a leaf node, we need to divide and create new leaf nodes
            if (this.IsLeaf)
            {
                //Check for the default point value in case this is the root of the treeWithUniqueValues
                if (this.IsRoot && this.HasValue == false)
                {
                    this.Point = insertingPoint;
                    this.Value = value;
                    this.HasValue = true;
                    Tree.PointAdded(this, insertingPoint, value);
                    //Tree.ValueToNodeTable.Add(this.Value, this);
                    return this;
                }
                //Check that the point we are being asked to insert is not a duplicate of our current point
                else if (this.Point == insertingPoint)
                {
                    throw new QuadTreeWithUniqueValues<T>.DuplicatePointException(insertingPoint);
                    //return null;
                }
                else // It is a new point.  We need to create children for this node and insert the points
                {
                    //First create a child for the existing point

                    //Remove ourselves from the table, we are going to become a branch and not a leaf.  This must be done before constructor
                    //Tree.ValueToNodeTable.Remove(this.Value);
                    Tree.PointRemoved(this, Point, Value);

                    Quadrant quad = GetQuad(Point);

                    AddNodeToQuadrant(quad, Point, Value);

                    //Erase our point just to be safe since we aren't a leaf anymore
                    this.Point = new Vector2();
                    this.Value = default;
                    this.HasValue = false;

                    //Call insert on ourselves to insert the new point
                    return this.Insert(insertingPoint, value);
                }
            }
            //If we are not a leaf node, insert into the appropriate quadrant if it exists
            else
            {
                Quadrant quad = GetQuad(insertingPoint);

                //If we haven't created a node for this quadrant then do so...
                if (_quadrants[(int)quad] is null)
                {
                    AddNodeToQuadrant(quad, insertingPoint, value);
                    return _quadrants[(int)quad];
                }
                else
                {
                    //If we have created a node for that quadrant then recursively call insert
                    return _quadrants[(int)quad].Insert(insertingPoint, value);
                }
            }
        }

        private void AddNodeToQuadrant(Quadrant quad, Vector2 insertingPoint, T value)
        {
            QuadTreeNode<T> newNode = new(this, quad, insertingPoint, value);

            //If value already exists in the treeWithUniqueValues this will fail
            //Tree.ValueToNodeTable.Add(value, newNode);
            Tree.PointAdded(newNode, insertingPoint, value);
            _quadrants[(int)quad] = newNode;
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

        internal bool ExpandBorder(in Vector2 point, out QuadTreeNode<T> new_root)
        {
            new_root = null;
            if (HasBorder && Border.Covers(point))
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
                    Vector2 BoxDistance = this.Point - point;
                    //Create a boundary centered on our root node that will cover the 2nd point
                    double quad_size = Math.Max(Math.Abs(BoxDistance.X * 2), Math.Abs(BoxDistance.Y * 2));
                    double rounded_quad_size = RoundUpToNearestPowerOfTen(quad_size);
                    Vector2 NewBoundsDims = new(rounded_quad_size, rounded_quad_size);


                    //Center the new boundary between the two points 
                    Vector2 Center = (this.Point + point) / 2;
                    Vector2 NewBoundsOrigin = Center - (NewBoundsDims / 2);

                    Rectangle Bounds = new(NewBoundsOrigin, NewBoundsOrigin + NewBoundsDims);
                    this.Border = Bounds;

                    Debug.Assert(Bounds.Covers(this.Point), "The border specified must include the node's point");
                    Debug.Assert(Bounds.Covers(point), "The border specified must include the new point");
                    if (Bounds.Covers(this.Point) == false)
                    {
                        throw new ArgumentException("The border specified must include the node's point");
                    }
                    if (Bounds.Covers(point) == false)
                    {
                        throw new ArgumentException("The border specified must include the new point");
                    }

                    new_root = this;

                    //Trace.WriteLine(string.Format("Calculated border of {0}", Bounds));
                    return true;
                }

                throw new ArgumentException("Unexpected code path reached in QuadTreeWithUniqueValues ExpandBorder");
            }

            //The border does not contain the point, so we need to expand it
            double ParentWidth = this.Border.Width * 2;
            double ParentHeight = this.Border.Height * 2;
            Quadrant insertquad = GetQuad(point);
            var
                    //We are the upper-right node of the new root.
                    ParentCenter = insertquad switch
                    {
                        Quadrant.LowerLeft => Border.LowerLeft,//We are the upper-right node of the new root.
                        Quadrant.LowerRight => Border.LowerRight,//We are the Upper-left node of the new root.
                        Quadrant.UpperLeft => Border.UpperLeft,//We are the Lower-Right node of the new root
                        Quadrant.UpperRight => Border.UpperRight,//We are the Lower-Left node of the new root
                        _ => throw new ArgumentException("Unexpected quadrant"),
                    };
            Rectangle parent_bounds = new(ParentCenter - new Vector2(this.Border.Width, this.Border.Height), ParentWidth, ParentHeight);

            QuadTreeNode<T> new_parent = new(this.Tree, parent_bounds);

            Debug.Assert(new_parent.GetQuad(this.Border.Center) == insertquad.Opposite(), "When expanding the border the existing and new points should be in opposite quadrants");
            new_parent[insertquad.Opposite()] = this;
            this.Parent = new_parent;

            Debug.Assert(Math.Abs(this.Border.Width - new_parent.Border.Width / 2) < Global.Epsilon, "New root node must be twice as wide as this node");
            Debug.Assert(Math.Abs(this.Border.Height - new_parent.Border.Height / 2) < Global.Epsilon, "New root node must be twice as tall as this node");
            //Trace.WriteLine(string.Format("Expanded border from {0} to {1}", this.Border, parent_bounds));
            /*
            Debug.Assert(parent_bounds.Contains(this.Center), "New root node must include center of this quad");
            if(parent_bounds.Contains(this.Center) == false)
            {
                throw new ArgumentException("New root node must include center of this quad");
            }

           */

            if (new_parent.ExpandBorder(in point, out new_root))
            {
                Debug.Assert((this.IsLeaf == false) || new_root.Border.Covers(Point), "New root node must include our point");
                Debug.Assert(new_root.Border.Covers(point), "New root node must include new point");
                return true;
            }
            else
            {
                new_root = new_parent;
                Debug.Assert((this.IsLeaf == false) || new_root.Border.Covers(Point), "New root node must include our point");
                Debug.Assert(new_root.Border.Covers(point), "New root node must include new point");
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
            {
                Tree.PointRemoved(node, node.Point, node.Value);
                //Tree.ValueToNodeTable.Remove(node.Value);
            }

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
                if (IsRoot == false)
                {
                    Parent.Remove(this);
                }
                else
                {
                    //Looks like we are the last node in the treeWithUniqueValues
                    //treeWithUniqueValues.ValueToNodeTable.Remove(this.Value);
                    //this.Value = default(T);
                    //this.HasValue = false;
                }
            }
        }

        public void Remove(Vector2 p, out T output)
        {
            if (this.IsRoot && this.HasValue && this.Point == p)
            {
                output = this.Value;
                this.Value = default;
                this.HasValue = false;
                Tree.PointRemoved(this, p, output);
                return;
            }

            if (this.IsLeaf)
            {
                throw new KeyNotFoundException($"{p} not in QuadTree to remove");
            }

            if (_quadrants[(int)GetQuad(p)] is QuadTreeNode<T> quad)
            {
                if (!quad.IsLeaf)
                    quad.Remove(p, out output); //Try to find the point in the child
                else
                {
                    if (quad.Point == p)
                    {
                        output = quad.Value;
                        this.Remove(quad);
                        return;
                    }
                    else
                    {
                        throw new KeyNotFoundException($"{p} not in QuadTree to remove");
                    }
                }
            }
            else
            { //We have no quadrant data for where the point falls
                throw new KeyNotFoundException($"{p} not in QuadTree to remove");
            }
        }

        public void Update(Vector2 point, T value)
        {
            if (this.IsLeaf)
            {
                if (this.HasValue && this.Point == point)
                {
                    this.Value = value;
                    return;
                }

                throw new KeyNotFoundException($"{point} not found in QuadTree to update with value {value}");
            }
            else
            {
                var quad = GetQuad(point);
                if (_quadrants[(int)quad] is not null)
                {
                    _quadrants[(int)quad].TryUpdate(point, value);
                }
                else
                {
                    throw new KeyNotFoundException($"{point} not found in QuadTree to update with value {value}");
                }
            }
        }

        public bool TryUpdate(Vector2 point, T value)
        {
            try
            {
                Update(point, value);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the value associated with the point nearest to the passed input parameter point
        /// </summary>
        /// <param name="point">Query point</param>
        /// <param name="nodePoint">Nearest point in QuadTreeWithUniqueValues to query point</param>
        /// <param name="distance">Distance from query point to nodePoint</param>
        /// <returns>Data value associated with nearest point</returns>
        public T FindNearest(Vector2 point, out Vector2 nodePoint, ref double distance)
        {
            if (this.IsLeaf)
            {
                if (this.IsRoot && HasValue == false)
                    throw new InvalidOperationException(
                        $"QuadTreeWithUniqueValues has no entries, so FindNearest cannot return a valid value");

                Debug.Assert(this.HasValue);
                distance = Vector2.Distance(in this.Point, in point);
                nodePoint = this.Point;
                return this.Value;
            }
            else
            {
                Quadrant quad = GetQuad(point);
                T retValue = default;
                nodePoint = new Vector2(double.MinValue, double.MinValue);

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

                Rectangle rect = new(point, distance);

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
                            T foundValue = _quadrants[iQuad].FindNearest(point, out Vector2 foundNode, ref newDistance);

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

                                rect = new Rectangle(point, distance);
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
        /// <param name="nodePoint">Nearest point in QuadTreeWithUniqueValues to query point</param>
        /// <param name="distance">Distance from query point to nodePoint</param>
        /// <returns>Data value associated with nearest point</returns>
        public bool FindNearestPoints(Vector2 point, int nPoints, ref FixedSizeDistanceList<T> distanceList)
        {
            if (nPoints == 0)
            {
                return false;
            }

            if (this.IsLeaf)
            {
                Debug.Assert(this.HasValue);
                double distance = Vector2.Distance(this.Point, point);

                return distanceList.TryAdd(new DistanceToPoint<T>(this.Point, distance, Value));
            }
            else
            {
                //Set to true if any child added a point
                bool PointFound = false;

                Quadrant quad = GetQuad(point);
                Vector2 nodePoint = new(double.MinValue, double.MinValue);

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

                Rectangle rect = new(point, maxDistance);

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
                                rect = new Rectangle(point, maxDistance);
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
        public void Intersect(in Rectangle RequestRect,
                                            bool NeedTest,
                                            ref List<Vector2> Points,
                                            ref List<T> Values)
        {
            if (this.IsLeaf)
            {
                if (this.HasValue == false)
                    return;


                if (NeedTest)
                {
                    if (RequestRect.Covers(Point))
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

                    if (Border.Intersects(in RequestRect))
                    {

                        if (RequestRect.Covers(Border))
                        {
                            this.UpperLeft?.Intersect(in RequestRect, false, ref Points, ref Values);
                            this.UpperRight?.Intersect(in RequestRect, false, ref Points, ref Values);
                            this.LowerLeft?.Intersect(in RequestRect, false, ref Points, ref Values);
                            this.LowerRight?.Intersect(in RequestRect, false, ref Points, ref Values);

                            return;
                        }
                        //else fall through to calls below requiring test
                    }
                    else
                        //Does not intersect.  Return empty list
                        return;
                }

                this.UpperLeft?.Intersect(in RequestRect, true, ref Points, ref Values);
                this.UpperRight?.Intersect(in RequestRect, true, ref Points, ref Values);
                this.LowerLeft?.Intersect(in RequestRect, true, ref Points, ref Values);
                this.LowerRight?.Intersect(in RequestRect, true, ref Points, ref Values);
                return;

            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
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
