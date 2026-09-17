using FsCheck;
using Geometry;
using GeometryTests.Algorithms;
using GeometryTests.FSCheck;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests.FSCheck
{

    /// <summary>
    /// A brute-force point searching class to compare our quad-treeWithUniqueValues implementation against
    /// Contains a tuples of Points and the associated value of the point in the quad treeWithUniqueValues
    /// </summary>
    internal class QuadTreeModel : List<PointTuple>
    {
        public List<DistanceToPoint<PointTuple>> Nearest(Vector2 point)
        {
            List<DistanceToPoint<PointTuple>> listPoints = [.. this.Select((p, i) => new DistanceToPoint<PointTuple>(p, Vector2.Distance(p, point), p))];
            listPoints.Sort(new DistanceToPointSorter<PointTuple>());
            return listPoints;
        }

        public bool Contains(Vector2 point) => this.Any(pt => pt.Point.Equals(point));
    }

    internal class QuadTreeWithUniqueValuesSpec : ICommandGenerator<QuadTreeWithUniqueValues<int>, QuadTreeModel>
    {
        public QuadTreeWithUniqueValuesSpec()
        {
        }

        public Geometry.QuadTreeWithUniqueValues<int> InitialActual => new();

        public QuadTreeModel InitialModel => [];

        public static Property ClassifySize(Property prop, int size)
        {
            return prop.ClassifySize(size)
                       .Trivial(size == 0);
        }

        public static Property TestFindNearestPoint(Vector2 Point, QuadTreeWithUniqueValues<int> treeWithUniqueValues, QuadTreeModel model)
        {
            //Does a brute force search of the model to ensure the correct points is returned from the treeWithUniqueValues
            var modelNearestList = model.Nearest(Point);

            ///////////////////////////////////////////
            ///Check that we can find the nearest point
            bool pointFound = treeWithUniqueValues.TryFindNearest(Point, out var treeNearestIndex, out double treeDistance);

            var modelNearest = modelNearestList.First();

            bool correctPointFound = modelNearest.Value.Value == treeNearestIndex;
            bool distanceMatched = modelNearest.Distance == treeDistance;
            ///////////////////////////////////////////
            return (pointFound.Label("Point found"))
                    .And(correctPointFound.Label("Nearest point found"))
                    .And(distanceMatched.Label("Nearest point distance matched"));
        }

        public static Property TestFindNearestPoints(Vector2 Point, int nPoints, QuadTreeWithUniqueValues<int> treeWithUniqueValues, QuadTreeModel model)
        {
            if (nPoints > model.Count)
                nPoints = model.Count;

            var modelNearestList = model.Nearest(Point);
            var treeNearestList = treeWithUniqueValues.FindNearestPoints(Point, nPoints);

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

        public Gen<Command<Geometry.QuadTreeWithUniqueValues<int>, QuadTreeModel>> Next(QuadTreeModel value)
        {
            if (value.Count == 0)
                return Vector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>);

            else
            {
                /*
                var command_generators = new Gen<Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>>[] { Vector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>),
                                                                              Gen.Zip(Vector2Generators.ArbRandomPoint().Generator, Arb.Default.Byte().Generator.Where(b => b <= value.Count)).Select((val) => new NearestPointsOperation(val.Item1, (int)val.Item2) as Command<Geometry.QuadTreeWithUniqueValues<int>, QuadTreeModel>) };

                return Gen.OneOf(command_generators);
                */

                return Gen.Frequency(
                    Tuple.Create(3, Vector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>)),
                    Tuple.Create(1, Gen.Choose(0, InitialModel.Count - 1 < 0 ? 0 : InitialModel.Count - 1).Select(i => new RemovePointOperation(value[i]) as Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>)),
                    Tuple.Create(1, Gen.Zip(Vector2Generators.ArbRandomPoint().Generator,
                                            Gen.Choose(0, InitialModel.Count))
                                                                        .Select((val) => new NearestPointsOperation(val.Item1, (int)val.Item2) as Command<Geometry.QuadTreeWithUniqueValues<int>, QuadTreeModel>)));
            }

            //Vector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>);
        }

        private class AddPointOperation(Vector2 point) : Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>
        {
            public readonly PointTuple Point = new(point, System.Threading.Interlocked.Increment(ref NextPointID));

            private static int NextPointID = -1;

            public bool AddResult { get; private set; } = false;

            public override Property Post(QuadTreeWithUniqueValues<int> treeWithUniqueValues, QuadTreeModel model)
            {
                bool value_found = treeWithUniqueValues.Contains(Point.Value);
                var findPoint = QuadTreeWithUniqueValuesSpec.TestFindNearestPoint(Point, treeWithUniqueValues, model);

                findPoint = QuadTreeWithUniqueValuesSpec.ClassifySize(findPoint, model.Count);

                var findPoints = QuadTreeWithUniqueValuesSpec.TestFindNearestPoints(Point, model.Count, treeWithUniqueValues, model);
                /*//Does a brute force search of the model to ensure the correct points is returned from the treeWithUniqueValues
                var modelNearestList = model.Nearest(Point);

                ///////////////////////////////////////////
                ///Check that we can find the nearest point
                int treeNearestIndex = treeWithUniqueValues.FindNearest(Point, out double treeDistance);

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
                             .And(value_found.Label("Inserted value not found in treeWithUniqueValues"));
                return QuadTreeWithUniqueValuesSpec.ClassifySize(output, model.Count);
                /*
                return (correctPointFound.Label("Nearest point found"))
                        .And(distanceMatched.Label("Nearest point distance matched"))
                        .And(pointsHaveMatchedDistance.Label("Points searched in order have matching distance"))
                        .And(pointsHaveMatchedIndex.Label("Points searched in order have matching indicies"))
                        .ClassifySize(model.Count)
                        .Trivial(model.Count == 0);
                        */
            }

            public override bool Pre(QuadTreeModel _arg1) =>
                //Do not attempt to add duplicate points
                _arg1.Contains(Point.Point) == false;

            public override QuadTreeWithUniqueValues<int> RunActual(QuadTreeWithUniqueValues<int> value)
            {
                AddResult = value.TryAdd(Point, Point.Value);
                return value;
            }

            public override QuadTreeModel RunModel(QuadTreeModel value)
            {
                value.Add(Point);
                return value;
            }

            public override string ToString() => "Add " + Point.ToString();
        }

        private class NearestPointsOperation(Vector2 point, int num_points) : Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>
        {
            public readonly Vector2 Point = point;
            public readonly int nPoints = num_points;

            public override Property Post(QuadTreeWithUniqueValues<int> treeWithUniqueValues, QuadTreeModel model)
            {
                //Does a brute force search of the model to ensure the correct points is returned from the treeWithUniqueValues                
                Property result = TestFindNearestPoints(Point, nPoints, treeWithUniqueValues, model)
                        .ClassifySize(nPoints)
                        .Trivial(nPoints == 0);


                return result;
            }

            public override bool Pre(QuadTreeModel _arg1) =>
                //Do not attempt to add duplicate points
                this.nPoints <= _arg1.Count;

            public override QuadTreeWithUniqueValues<int> RunActual(QuadTreeWithUniqueValues<int> value) => value;

            public override QuadTreeModel RunModel(QuadTreeModel value) => value;

            public override string ToString() => string.Format("Find nearest {0} points to {1} ", this.nPoints, Point);
        }

        /// <summary>
        /// Removes a random point from the quad treeWithUniqueValues
        /// </summary>
        private class RemovePointOperation(PointTuple value) : Command<QuadTreeWithUniqueValues<int>, QuadTreeModel>
        {
            /// <summary>
            /// The point being removed
            /// </summary>
            public PointTuple Point { get; private set; } = value;


            /// <summary>
            /// The returned value when the point was removed from the quad treeWithUniqueValues
            /// </summary>
            public bool RemovedFromQuadTree { get; private set; } = false;

            public override bool Pre(QuadTreeModel _arg1) => true;

            public override Property Post(QuadTreeWithUniqueValues<int> treeWithUniqueValues, QuadTreeModel model)
            {
                bool TreeRemovedPoint = false == treeWithUniqueValues.Contains(Point);
                bool TreeRemovedValue = false == treeWithUniqueValues.Contains(Point.Value);

                //Does a brute force search of the model to ensure the correct points is returned from the treeWithUniqueValues                
                Property result = (TreeRemovedPoint.Label($"treeWithUniqueValues contains removed point {Point}"))
                                  .And(TreeRemovedValue.Label($"treeWithUniqueValues contains removed value {Point}"))
                                  .ClassifySize(model.Count);

                return result;
            }

            public override QuadTreeWithUniqueValues<int> RunActual(QuadTreeWithUniqueValues<int> value)
            {
                RemovedFromQuadTree = value.TryRemove(Point.Value, out int removed);
                return value;
            }

            public override QuadTreeModel RunModel(QuadTreeModel value)
            {
                value.Remove(Point);
                return value;
            }

            public override string ToString() => string.Format($"Remove {Point}");
        }
    }
}
