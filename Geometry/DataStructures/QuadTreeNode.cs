using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;

namespace Geometry
{
        internal class QuadTreeNode<T>
        {
            readonly QuadTree<T> Tree;
            internal readonly QuadTreeNode<T> Parent = null;

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

            private QuadTreeNode<T>[] _quadrants = new QuadTreeNode<T>[] { null, null, null, null };

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

            internal readonly GridRectangle Border;
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
                    else if (this.Point == point)
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
                    if (Parent != null)
                    {
                        Parent.Remove(this);
                    }
                    else
                    {
                        //Looks like we are the last node in the tree
                        Tree.ValueToNodeTable.Remove(this.Value);
                        this.Value = default(T);
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
            public bool FindNearestPoints(GridVector2 point, int nPoints, ref SortedList<double, T> distanceList)
            {
                
                if (this.IsLeaf)
                {
                    Debug.Assert(this.HasValue);
                    double distance = GridVector2.Distance(this.Point, point);
                    
                    //This is a sorted list, so don't add if our distance is greater than the nth item.
                    if (distanceList.Keys.Count < nPoints)
                    {
                        distanceList.Add(distance, this.Value);
                        return true;
                    }
                    if (distance < distanceList.Keys[nPoints - 1])
                    {
                        distanceList.Add(distance, this.Value);
                        distanceList.RemoveAt(nPoints);
                        return true; 
                    }

                    return false;
                }
                else
                {
                    //Set to true if any child added a point
                    bool PointAdded = false; 

                    
                    Quadrant quad = GetQuad(point);
                    GridVector2 nodePoint = new GridVector2(double.MinValue, double.MinValue);

                    //If we aren't a leaf node then do a depth first search to find the nearest point
                    if (_quadrants[(int)quad] != null)
                    {
                        PointAdded = _quadrants[(int)quad].FindNearestPoints(point, nPoints, ref distanceList);
                    }
                    
                    //Next we check our other quadrants to see if it is possible they could have a closer point
                    //It is OK if we didn't have a quadrant for the point in the earlier check because then the default values for 
                    //distance force the adjacent quadrants to be checked

                    double maxDistance = double.MaxValue;
                    
                    if (distanceList.Count >= nPoints)
                        maxDistance = distanceList.Keys[nPoints - 1];                      

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
                            if (_quadrants[iQuad].Border.Intersects(rect))
                            {
                                bool ListChanged = _quadrants[iQuad].FindNearestPoints(point, nPoints, ref distanceList);

                                if (ListChanged)
                                {
                                    PointAdded = true; 

                                    if (distanceList.Count < nPoints)
                                        maxDistance = distanceList.Keys[distanceList.Count - 1];
                                    else
                                        maxDistance = distanceList.Keys[nPoints - 1];

                                    rect = new GridRectangle(point, maxDistance);
                                }
                            }
                        }
                    }

                    //OK, we have the best value we can
                    return PointAdded;
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
    }
}
