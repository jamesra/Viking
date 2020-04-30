using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using GeometryTests;
using FsCheck;
using GeometryTests.Algorithms;

namespace GeometryTests
{
    /// <summary>
    /// A brute-force point searching class to compare our quad-tree implementation against
    /// </summary>
    internal class QuadTreeModel : List<GridVector2>
    {
        public List<DistanceToPoint<int>> Nearest(GridVector2 point)
        {
            var listPoints = this.Select((p, i) => new DistanceToPoint<int>(p, GridVector2.Distance(p, point), i)).ToList();
            listPoints.Sort(new DistanceToPointSorter<int>());
            return listPoints;
        }
    }

    internal class QuadTreeSpec : ICommandGenerator<QuadTree<int>, QuadTreeModel>
    {
        List<GridVector2> Points;
        int iAddedPoints;

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

            bool correctPointFound = modelNearest.Value == treeNearestIndex;
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

                pointIndexMatched[i] = treePoint.Value == modelPoint.Value;
                pointDistanceMatched[i] = treePoint.Distance == modelPoint.Distance;

                if (pointIndexMatched[i] == false && pointDistanceMatched[i] == true)
                {
                    //Check for a matching index at the exact same distance
                    var candidates = modelNearestList.Where(d => d.Distance == treePoint.Distance);
                    pointIndexMatched[i] = candidates.Any(c => c.Value == treePoint.Value);
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
                    Tuple.Create(3,  GridVector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTree<int>, QuadTreeModel>)),
                    Tuple.Create(1, Gen.Zip(GridVector2Generators.ArbRandomPoint().Generator, Arb.Default.Byte().Generator.Where(b => b <= value.Count)).Select((val) => new NearestPointsOperation(val.Item1, (int)val.Item2) as Command<Geometry.QuadTree<int>, QuadTreeModel>)));
            }

            //GridVector2Generators.ArbRandomPoint().Generator.Select(p => new AddPointOperation(p) as Command<QuadTree<int>, QuadTreeModel>);
        }

        public class AddPointOperation : Command<QuadTree<int>, QuadTreeModel>
        {
            public readonly GridVector2 Point;

            public AddPointOperation(GridVector2 point)
            {
                Point = point;
            }
             
            public override Property Post(QuadTree<int> tree, QuadTreeModel model)
            {
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

                var output = findPoint.And(findPoints);
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
                return _arg1.Contains(Point) == false;
            }

            public override QuadTree<int> RunActual(QuadTree<int> value)
            {
                value.Add(Point, value.Count);
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

        public class NearestPointsOperation : Command<QuadTree<int>, QuadTreeModel>
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
    }
}
