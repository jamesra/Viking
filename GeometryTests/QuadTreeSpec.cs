using FsCheck;
using Geometry;
using GeometryTests.Algorithms;
using GeometryTests.FSCheck;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    internal class PointTuple : Tuple<GridVector2, int>, IEquatable<PointTuple>
    {
        public PointTuple(GridVector2 item1, int item2) : base(item1, item2)
        {
        }

        public GridVector2 Point => this.Item1;
        public int Value => this.Item2;

        public bool Equals(PointTuple other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return other.Point.Equals(this.Point) && other.Value.Equals(this.Value);
        }

        public static implicit operator GridVector2(PointTuple t) => t.Point;

        public override string ToString()
        {
            return $"{Point} : {Value}";
        }
    }

    /// <summary>
    /// A brute-force point searching class to compare our quad-tree implementation against
    /// Contains a tuples of Points and the associated value of the point in the quad tree
    /// </summary>
    internal class QuadTreeModel : List<PointTuple>
    {
        public List<DistanceToPoint<PointTuple>> Nearest(GridVector2 point)
        {
            var listPoints = this.Select((p, i) => new DistanceToPoint<PointTuple>(p, GridVector2.Distance(p, point), p)).ToList();
            listPoints.Sort(new DistanceToPointSorter<PointTuple>());
            return listPoints;
        }

        public bool Contains(GridVector2 point)
        {
            return this.Any(pt => pt.Point.Equals(point));
        }
    }

    internal class QuadTreeSpec : ICommandGenerator<QuadTree<int>, QuadTreeModel>
    {  
        public QuadTreeSpec()
        {
        }

        public Geometry.QuadTree<int> InitialActual => new QuadTree<int>();

        public QuadTreeModel InitialModel => new QuadTreeModel();

        public static Property ClassifySize(Property prop, int size)
        {
            return prop.ClassifySize(size)
                       .Trivial(size == 0);
        }

        public static Property TestFindNearestPoint(GridVector2 Point, QuadTree<int> tree, QuadTreeModel model)
        {
            //Does a brute force search of the model to ensure the correct points is returned from the tree
            var modelNearestList = model.Nearest(Point);

            ///////////////////////////////////////////
            ///Check that we can find the nearest point
            int treeNearestIndex = tree.FindNearest(Point, out double treeDistance);

            var modelNearest = modelNearestList.First();

            bool correctPointFound = modelNearest.Value.Value == treeNearestIndex;
            bool distanceMatched = modelNearest.Distance == treeDistance;
            ///////////////////////////////////////////
            ///
            return (correctPointFound.Label("Nearest point found"))
                    .And(distanceMatched.Label("Nearest point distance matched"));
        }

        public static Property TestFindNearestPoints(GridVector2 Point, int nPoints, QuadTree<int> tree, QuadTreeModel model)
        {
            if (nPoints > model.Count)
                nPoints = model.Count;

            var modelNearestList = model.Nearest(Point);
            var treeNearestList = tree.FindNearestPoints(Point, nPoints);

            bool pointsFoundCountMatched = treeNearestList.Count >= nPoints;

            if (pointsFoundCountMatched == false)
            {
                return (pointsFoundCountMatched.Label("Returned requested number of points or more"))
                        .ClassifySize(modelNearestList.Count);
            }

            bool[] pointIndexMatched = new bool[treeNearestList.Count];
            bool[] pointDistanceMatched = new bool[treeNearestList.Count];
            for (int i = 0; i < treeNearestList.Count; i++)
            {
                var treePoint = treeNearestList[i];
                var modelPoint = modelNearestList[i];

                pointIndexMatched[i] = treePoint.Value == modelPoint.Value.Value;
                pointDistanceMatched[i] = treePoint.Distance == modelPoint.Distance;

                if (pointIndexMatched[i] == false && pointDistanceMatched[i] == true)
                {
                    //Check for a matching index at the exact same distance
                    var candidates = modelNearestList.Where(d => d.Distance == treePoint.Distance);
                    pointIndexMatched[i] = candidates.Any(c => c.Value.Value == treePoint.Value);
                }
            }

            bool pointsHaveMatchedIndex = false == pointIndexMatched.Any(p => p == false);
            bool pointsHaveMatchedDistance = false == pointDistanceMatched.Any(p => p == false);

            bool pass = pointsHaveMatchedDistance && pointsHaveMatchedIndex && pointsFoundCountMatched;

            return (pointsHaveMatchedDistance.Label("Points searched in order have matching distance"))
                    .And(pointsHaveMatchedIndex.Label("Points searched in order have matching indicies"))
                    .And(pointsFoundCountMatched.Label("Returned requested number of points or more"));

        }

        public Gen<Command<Geometry.QuadTree<int>, QuadTreeModel>> Next(QuadTreeModel value)
        {
            if (value.Count == 0)
                return GridVector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTree<int>, QuadTreeModel>);

            else
            {
                /*
                var command_generators = new Gen<Command<QuadTree<int>, QuadTreeModel>>[] { GridVector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTree<int>, QuadTreeModel>),
                                                                              Gen.Zip(GridVector2Generators.ArbRandomPoint().Generator, Arb.Default.Byte().Generator.Where(b => b <= value.Count)).Select((val) => new NearestPointsOperation(val.Item1, (int)val.Item2) as Command<Geometry.QuadTree<int>, QuadTreeModel>) };

                return Gen.OneOf(command_generators);
                */

                return Gen.Frequency(
                    Tuple.Create(3, GridVector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTree<int>, QuadTreeModel>)),
                    Tuple.Create(1, Gen.Choose(0, InitialModel.Count-1 < 0 ? 0 : InitialModel.Count).Select(i => new RemovePointOperation(value[i]) as Command<QuadTree<int>, QuadTreeModel>)),
                    Tuple.Create(1, Gen.Zip(GridVector2Generators.ArbRandomPoint().Generator,
                                            Gen.Choose(0, InitialModel.Count))
                                                                        .Select((val) => new NearestPointsOperation(val.Item1, (int)val.Item2) as Command<Geometry.QuadTree<int>, QuadTreeModel>)));
            }

            //GridVector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTree<int>, QuadTreeModel>);
        }

        private class AddPointOperation : Command<QuadTree<int>, QuadTreeModel>
        {
            public readonly PointTuple Point;

            private static int NextPointID = -1;

            public bool AddResult { get; private set; } = false;

            public AddPointOperation(GridVector2 point)
            {
                Point = new PointTuple(point, System.Threading.Interlocked.Increment(ref NextPointID));
            }

            public override Property Post(QuadTree<int> tree, QuadTreeModel model)
            {
                bool value_found = tree.Contains(Point.Value);
                var findPoint = QuadTreeSpec.TestFindNearestPoint(Point, tree, model);

                findPoint = QuadTreeSpec.ClassifySize(findPoint, model.Count);

                var findPoints = QuadTreeSpec.TestFindNearestPoints(Point, model.Count, tree, model);
                /*//Does a brute force search of the model to ensure the correct points is returned from the tree
                var modelNearestList = model.Nearest(Point);

                ///////////////////////////////////////////
                ///Check that we can find the nearest point
                int treeNearestIndex = tree.FindNearest(Point, out double treeDistance);

                var modelNearest = modelNearestList.First();

                bool correctPointFound = modelNearest.Value == treeNearestIndex;
                bool distanceMatched = modelNearest.Distance == treeDistance;
                ///////////////////////////////////////////
                
                ///////////////////////////////////////////////
                /// Check that we can find the N nearest points

                
                ///////////////////////////////////////////////
                bool pass = correctPointFound && distanceMatched && pointsHaveMatchedDistance && pointsHaveMatchedIndex && pointsFoundCountMatched;
                */

                var output = findPoint.And(findPoints)
                             .And(AddResult.Label("TryAdd result did not indicate success"))
                             .And(value_found.Label("Inserted value not found in tree"));
                return QuadTreeSpec.ClassifySize(output, model.Count);
                /*
                return (correctPointFound.Label("Nearest point found"))
                        .And(distanceMatched.Label("Nearest point distance matched"))
                        .And(pointsHaveMatchedDistance.Label("Points searched in order have matching distance"))
                        .And(pointsHaveMatchedIndex.Label("Points searched in order have matching indicies"))
                        .ClassifySize(model.Count)
                        .Trivial(model.Count == 0);
                        */
            }
             
            public override bool Pre(QuadTreeModel _arg1)
            {
                //Do not attempt to add duplicate points
                return _arg1.Contains(Point.Point) == false;
            }

            public override QuadTree<int> RunActual(QuadTree<int> value)
            {
                AddResult = value.TryAdd(Point, Point.Value);
                return value;
            }

            public override QuadTreeModel RunModel(QuadTreeModel value)
            {
                value.Add(Point);
                return value;
            }

            public override string ToString()
            {
                return "Add " + Point.ToString();
            }
        }

        private class NearestPointsOperation : Command<QuadTree<int>, QuadTreeModel>
        {
            public readonly GridVector2 Point;
            public readonly int nPoints;

            public NearestPointsOperation(GridVector2 point, int num_points)
            {
                Point = point;
                nPoints = num_points;
            }

            public override Property Post(QuadTree<int> tree, QuadTreeModel model)
            {
                //Does a brute force search of the model to ensure the correct points is returned from the tree                
                Property result = TestFindNearestPoints(Point, nPoints, tree, model)
                        .ClassifySize(nPoints)
                        .Trivial(nPoints == 0);


                return result;
            }

            public override bool Pre(QuadTreeModel _arg1)
            {
                //Do not attempt to add duplicate points
                return this.nPoints <= _arg1.Count;
            }

            public override QuadTree<int> RunActual(QuadTree<int> value)
            {
                return value;
            }

            public override QuadTreeModel RunModel(QuadTreeModel value)
            {
                return value;
            }

            public override string ToString()
            {
                return string.Format("Find nearest {0} points to {1} ", this.nPoints, Point);
            }
        }

        /// <summary>
        /// Removes a random point from the quad tree
        /// </summary>
        private class RemovePointOperation : Command<QuadTree<int>, QuadTreeModel>
        {
            /// <summary>
            /// The point being removed
            /// </summary>
            public PointTuple Point {get; private set;}
             

            /// <summary>
            /// The returned value when the point was removed from the quad tree
            /// </summary>
            public bool RemovedFromQuadTree { get; private set; } = false;

            public RemovePointOperation(PointTuple value)
            {
                Point = value; 
            }

            public override bool Pre(QuadTreeModel _arg1)
            { 
                return true;
            }

            public override Property Post(QuadTree<int> tree, QuadTreeModel model)
            {
                bool TreeRemovedPoint = false == tree.Contains(Point);
                bool TreeRemovedValue = false == tree.Contains(Point.Value);

                //Does a brute force search of the model to ensure the correct points is returned from the tree                
                Property result = (TreeRemovedPoint.Label($"Tree contains removed point {Point}"))
                                  .And(TreeRemovedValue.Label($"Tree contains removed value {Point}"))
                                  .ClassifySize(model.Count); 

                return result;
            }
              
            public override QuadTree<int> RunActual(QuadTree<int> value)
            {
                RemovedFromQuadTree = value.TryRemove(Point.Value, out int removed);
                return value;
            }

            public override QuadTreeModel RunModel(QuadTreeModel value)
            {
                value.Remove(Point);
                return value;
            }

            public override string ToString()
            {
                return string.Format($"Remove {Point}");
            }
        }
    }
}
