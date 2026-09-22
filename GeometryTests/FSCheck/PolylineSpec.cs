using FsCheck;
using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryTests.FSCheck
{
    /// <summary>
    /// A list of points that increase along one axis with random values on the 2nd axis;
    /// </summary>
    internal class LinearPolylineModel(Axis axis) : SortedSet<PointTuple>(new PointTupleComparer(axis))
    {
        public Axis Axis = axis;

        public bool Contains(Vector2 point) => this.Any(pt => pt.Point.Equals(point));

        public Rectangle BoundingRect => this.Select(p => p.Point).BoundingBox();
    }

    class PolylineSpec(Axis axis) : ICommandGenerator<Polyline, LinearPolylineModel>
    {
        public Polyline InitialActual => new(false);

        public LinearPolylineModel InitialModel => new(Axis);

        private readonly Axis Axis = axis;

        public Gen<Command<Polyline, LinearPolylineModel>> Next(LinearPolylineModel value)
        {
            if (value.Count < 2)
            {
                return Gen.Zip(Gen.Choose(0, value.Count < 0 ? 0 : value.Count),
                               Gen.Choose(1, 9).Select(v => (double)v / 10.0),
                               Arb.Default.NormalFloat().Generator
                               ) //Todo: Limit range to 0 
                          .Select((val) => new InsertPointOperation(val.Item1, val.Item2, val.Item3.Get, Axis) as Command<Polyline, LinearPolylineModel>);
            }
            else
            {
                return Gen.Frequency(
                    Tuple.Create(1, Gen.Zip(Gen.Choose(0, value.Count < 0 ? 0 : value.Count),
                                                   Gen.Choose(1, 9).Select(v => (double)v / 10.0),  //We do not use 0 to 10 in this test because the Y value is random so it is hard to know if self intersection should occur during the test
                                                   Arb.Default.NormalFloat().Generator
                                                   )
                                                   .Select((val) => new InsertPointOperation(val.Item1, val.Item2, val.Item3.Get, Axis) as Command<Polyline, LinearPolylineModel>)),
                    Tuple.Create(1, Gen.Zip(Gen.Choose(0, value.Count - 2 < 0 ? 0 : value.Count - 2),
                                            Gen.Choose(0, 10).Select(v => (double)v / 10.0))
                                            .Select((val) => new IntersectLineTestOperation(val.Item1, val.Item2, Axis) as Command<Polyline, LinearPolylineModel>))

                    );
            }

        }
    }

    class InsertPointOperation : Command<Polyline, LinearPolylineModel>
    {
        private static int NextPointID = -1;

        readonly int InsertIndex;
        readonly int InsertID;
        readonly double InterpolationFraction;
        readonly double OffAxisValue;
        readonly Axis Axis;

        Property InsertToActualExpectedResult;

        PointTuple InsertValue;

        public InsertPointOperation(int index, double fraction, double off_axis_value, Axis axis)
        {
            InsertIndex = index;
            InsertID = System.Threading.Interlocked.Increment(ref NextPointID);
            InterpolationFraction = fraction;
            OffAxisValue = off_axis_value;
            Axis = axis;
            Debug.Assert(fraction > 0 && fraction < 1.0);
        }

        public override Property Post(Polyline polyline, LinearPolylineModel model)
        {
            bool SameLength = polyline.PointCount == model.Count;
            bool AllMatchInOrder = false == model.Select((p, i) => p.Point != polyline.Points[i]).Any(p => p == true);

            return (SameLength.Label("Same length"))
                   .And(AllMatchInOrder.Label("All points match in order"))
                   .And(InsertToActualExpectedResult);
        }

        public override bool Pre(LinearPolylineModel model)
        {
            double OnAxisValue; //Where on the axis the new point is inserted
            double InterpolationScalar = this.InterpolationFraction;

            if (model.Count == 0)
            {
                InsertValue = new PointTuple(new Vector2(InterpolationFraction, OffAxisValue), InsertID);
                return true;
            }
            else if (model.Count == 1)
            {
                //Insert point before/after the existing point with no interpolation
                double origin = model.First().Point[Axis];
                OnAxisValue = InsertIndex == 0 ? origin - (InterpolationScalar + 1) : origin + InterpolationScalar + 1;

                InsertValue = new PointTuple(new Vector2(OnAxisValue, OffAxisValue), InsertID);
                return true;
            }
            else
            {
                PointTuple[] modelPoints = [.. model];

                PointTuple OffsetOrigin;
                PointTuple Adjacent;

                double Offset;

                //Calculate the position of the point we will insert
                if (InsertIndex >= model.Count)
                {
                    //Place the point past the last point in the polyline
                    OffsetOrigin = modelPoints[modelPoints.Length - 1];
                    Adjacent = modelPoints[modelPoints.Length - 2];
                    //InterpolationScalar += 1;
                    Offset = 10 * InterpolationScalar;
                }
                else if (InsertIndex == 0)
                {
                    //Place the point before the first point in the polyline
                    OffsetOrigin = modelPoints[0];
                    Adjacent = modelPoints[1];
                    //InterpolationScalar = -(1 + InterpolationScalar);
                    Offset = -10 * InterpolationScalar;

                }
                else
                {
                    OffsetOrigin = modelPoints[InsertIndex - 1];
                    Adjacent = modelPoints[InsertIndex];

                    double Delta = Math.Abs(OffsetOrigin.Point[Axis] - Adjacent.Point[Axis]);
                    Offset = Delta * InterpolationScalar;
                    {
                        //Ensure the point we want to insert is not going to cause strange effects by being less than epsilon distance between other points
                        double OtherDirectionOffset = Delta * (1 - InterpolationScalar);

                        if (Math.Abs(Offset) <= Geometry.Global.Epsilon ||
                           Math.Abs(OtherDirectionOffset) <= Geometry.Global.Epsilon)
                        {
                            Trace.WriteLine($"Inserting @{InsertIndex} below epsilon distance");
                            return false;
                        }

                    }
                }

                OnAxisValue = OffsetOrigin.Point[Axis] + Offset;

                InsertValue = new PointTuple(new Vector2(OnAxisValue, OffAxisValue), InsertID);

                if (InsertIndex < model.Count)
                    Debug.Assert(InsertValue.Point[Axis] < modelPoints[InsertIndex].Point[Axis], $"New point {InsertValue.Point[Axis]} is to the right or equal to the point {modelPoints[InsertIndex].Point[Axis]} at the inserted index {InsertIndex}");
                else
                    Debug.Assert(InsertValue.Point[Axis] > modelPoints.Last().Point[Axis], $"New point {InsertValue.Point[Axis]} is to the left or equal to the point {modelPoints.Last().Point[Axis]} at the inserted index {InsertIndex}");

                return true;
            }
        }

        public override Polyline RunActual(Polyline value)
        {
            Trace.WriteLine($"Insert: {InsertID}@{this.InsertIndex} {this.InterpolationFraction} {InsertValue.Point} - Polyline Length: {value.NumUniqueVertices}");
            try
            {
                value.Insert(InsertIndex, InsertValue.Point);
                InsertToActualExpectedResult = true.Label("Inserted point without exception");
            }
            catch (Exception e)
            {
                InsertToActualExpectedResult = false.Label($"Exception inserting point: {e}");
#if DEBUG
                //throw;
#endif
            }
            return value;
        }

        public override LinearPolylineModel RunModel(LinearPolylineModel value)
        {
            Trace.WriteLine($"Insert to model: {InsertID}@{this.InsertIndex} {this.InterpolationFraction} {InsertValue.Point} - model Length: {value.Count}");
            bool added = value.Add(InsertValue);
            return value;
        }

        public override string ToString() => $"Insert {InsertID}@{InsertIndex} Fraction {InterpolationFraction}";
    }

    /// <summary>
    /// Checks if we can intersect the line segment of the polyline starting at the provided index
    /// </summary>
    class IntersectLineTestOperation(int index, double fraction, Axis axis) : Command<Polyline, LinearPolylineModel>
    {
        readonly int TestIndex = index;
        readonly double InterpolationFraction = fraction;
        readonly Axis Axis = axis;
        readonly Axis OffAxis = axis == Axis.X ? Axis.Y : Axis.X;
        Vector2 ExpectedIntersection;
        LineSegment TestSegment;

        public override bool Pre(LinearPolylineModel model)
        {
            if (model.Count < 2)
            {
                return false;
            }
            else
            {
                PointTuple[] modelPoints = [.. model];

                PointTuple Origin;
                PointTuple Adjacent;

                //Calculate the position of the point we will insert

                Origin = modelPoints[TestIndex];
                Adjacent = modelPoints[TestIndex + 1];

                //Where we expect the intersection to occur
                Vector2 ExpectedIntersection = (Adjacent.Point - Origin.Point) * InterpolationFraction;
                ExpectedIntersection += Origin.Point;

                Vector2 TestOrigin = ExpectedIntersection;
                Vector2 TestAdjacent = ExpectedIntersection;

                TestOrigin = OffsetAxis(TestOrigin, OffAxis, -TestOrigin[OffAxis] / 2.0);
                TestAdjacent = OffsetAxis(TestAdjacent, OffAxis, TestAdjacent[OffAxis] / 2.0);

                TestSegment = new LineSegment(TestOrigin, TestAdjacent);
                return true;
            }

            static Vector2 OffsetAxis(Vector2 point, Axis axis, double delta) =>
                axis == Axis.X
                    ? new Vector2(point.X + delta, point.Y)
                    : new Vector2(point.X, point.Y + delta);
        }

        public override Property Post(Polyline polyline, LinearPolylineModel model)
        {
            bool FoundIntersection = polyline.Intersects(TestSegment);
            return (FoundIntersection.Label("Find intersection in polyline"));
        }

        public override Polyline RunActual(Polyline value) => value;

        public override LinearPolylineModel RunModel(LinearPolylineModel value) => value;

        public override string ToString() => $"Find {TestIndex} @ Fraction {InterpolationFraction}";
    }
}
