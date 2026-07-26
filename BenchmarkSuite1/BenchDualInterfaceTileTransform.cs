using System;
using System.Collections.Generic;
using Geometry;
using Geometry.Transforms;

namespace VolumeModel.Benchmarks
{
    /// <summary>
    /// Minimal mosaic-style transform implementing both continuous and triangulation interfaces
    /// (same pattern as <see cref="DiscreteTransformWithContinuousFallback"/>).
    /// </summary>
    public sealed class BenchDualInterfaceTileTransform : IContinuousTransform, IControlPointTriangulation, ITransformInfo
    {
        private static readonly MappingGridVector2[] UnitSquareMapPoints =
        [
            new(new GridVector2(0, 0), new GridVector2(0, 0)),
            new(new GridVector2(1024, 0), new GridVector2(1024, 0)),
            new(new GridVector2(0, 1024), new GridVector2(0, 1024)),
            new(new GridVector2(1024, 1024), new GridVector2(1024, 1024)),
        ];

        public BenchDualInterfaceTileTransform(TileTransformInfo info, double offsetX, double offsetY)
        {
            Info = info;
            var offset = new GridVector2(offsetX, offsetY);
            MapPoints =
            [
                new(new GridVector2(0, 0) + offset, new GridVector2(0, 0) + offset),
                new(new GridVector2(1024, 0) + offset, new GridVector2(1024, 0) + offset),
                new(new GridVector2(0, 1024) + offset, new GridVector2(0, 1024) + offset),
                new(new GridVector2(1024, 1024) + offset, new GridVector2(1024, 1024) + offset),
            ];
            ControlBounds = new GridRectangle(offset, 1024, 1024);
            MappedBounds = ControlBounds;
        }

        public TransformBasicInfo Info { get; set; }

        public MappingGridVector2[] MapPoints { get; }

        public GridRectangle ControlBounds { get; }

        public GridRectangle MappedBounds { get; }

        public int[] TriangleIndicies { get; } = [0, 1, 2, 0, 2, 3];

        public List<int>[] Edges { get; } = [];

        public void Translate(in GridVector2 vector) { }

        public GridVector2 Transform(in GridVector2 point) => point;

        public GridVector2[] Transform(in GridVector2[] points)
        {
            var copy = new GridVector2[points.Length];
            Array.Copy(points, copy, points.Length);
            return copy;
        }

        public GridVector2 InverseTransform(in GridVector2 point) => point;

        public GridVector2[] InverseTransform(in GridVector2[] points) => Transform(points);

        public bool CanTransform(in GridVector2 point) => true;

        public bool TryTransform(in GridVector2 point, out GridVector2 v)
        {
            v = point;
            return true;
        }

        public bool[] TryTransform(in GridVector2[] points, out GridVector2[] v)
        {
            v = Transform(points);
            return new bool[points.Length];
        }

        public bool CanInverseTransform(in GridVector2 point) => true;

        public bool TryInverseTransform(in GridVector2 point, out GridVector2 v)
        {
            v = point;
            return true;
        }

        public bool[] TryInverseTransform(in GridVector2[] points, out GridVector2[] v)
        {
            v = InverseTransform(points);
            return new bool[points.Length];
        }

        public List<MappingGridVector2> IntersectingControlRectangle(in GridRectangle gridRect) =>
            new(UnitSquareMapPoints);

        public List<MappingGridVector2> IntersectingMappedRectangle(in GridRectangle gridRect) =>
            new(UnitSquareMapPoints);
    }
}
