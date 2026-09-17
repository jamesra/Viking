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
        private static readonly MappingVector2[] UnitSquareMapPoints =
        [
            new(new Vector2(0, 0), new Vector2(0, 0)),
            new(new Vector2(1024, 0), new Vector2(1024, 0)),
            new(new Vector2(0, 1024), new Vector2(0, 1024)),
            new(new Vector2(1024, 1024), new Vector2(1024, 1024)),
        ];

        public BenchDualInterfaceTileTransform(TileTransformInfo info, double offsetX, double offsetY)
        {
            Info = info;
            var offset = new Vector2(offsetX, offsetY);
            MapPoints =
            [
                new(new Vector2(0, 0) + offset, new Vector2(0, 0) + offset),
                new(new Vector2(1024, 0) + offset, new Vector2(1024, 0) + offset),
                new(new Vector2(0, 1024) + offset, new Vector2(0, 1024) + offset),
                new(new Vector2(1024, 1024) + offset, new Vector2(1024, 1024) + offset),
            ];
            ControlBounds = new Rectangle(offset, 1024, 1024);
            MappedBounds = ControlBounds;
        }

        public TransformBasicInfo Info { get; set; }

        public MappingVector2[] MapPoints { get; }

        public Rectangle ControlBounds { get; }

        public Rectangle MappedBounds { get; }

        public int[] TriangleIndicies { get; } = [0, 1, 2, 0, 2, 3];

        public List<int>[] Edges { get; } = [];

        public void Translate(in Vector2 vector) { }

        public Vector2 Transform(in Vector2 point) => point;

        public Vector2[] Transform(in Vector2[] points)
        {
            var copy = new Vector2[points.Length];
            Array.Copy(points, copy, points.Length);
            return copy;
        }

        public Vector2 InverseTransform(in Vector2 point) => point;

        public Vector2[] InverseTransform(in Vector2[] points) => Transform(points);

        public bool CanTransform(in Vector2 point) => true;

        public bool TryTransform(in Vector2 point, out Vector2 v)
        {
            v = point;
            return true;
        }

        public bool[] TryTransform(in Vector2[] points, out Vector2[] v)
        {
            v = Transform(points);
            return new bool[points.Length];
        }

        public bool CanInverseTransform(in Vector2 point) => true;

        public bool TryInverseTransform(in Vector2 point, out Vector2 v)
        {
            v = point;
            return true;
        }

        public bool[] TryInverseTransform(in Vector2[] points, out Vector2[] v)
        {
            v = InverseTransform(points);
            return new bool[points.Length];
        }

        public List<MappingVector2> IntersectingControlRectangle(in Rectangle gridRect) =>
            new(UnitSquareMapPoints);

        public List<MappingVector2> IntersectingMappedRectangle(in Rectangle gridRect) =>
            new(UnitSquareMapPoints);
    }
}
