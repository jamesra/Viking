using System;

namespace Geometry
{
    /// <summary>
    /// A half-infinite ray in 3D used for picking. <see cref="Direction"/> is normalized on construction so
    /// the parametric distances returned by the intersection routines are true world distances and can be
    /// compared between rays that were transformed into different local spaces by rigid transforms.
    /// </summary>
    [Serializable]
    public readonly struct Ray3D : IEquatable<Ray3D>
    {
        public readonly Vector3 Origin;

        public readonly Vector3 Direction;

        public Ray3D(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = Vector3.Normalize(direction);
        }

        /// <summary>Point at parametric distance <paramref name="distance"/> along the ray.</summary>
        public Vector3 PointAt(double distance) => Origin + (Direction * distance);

        public override string ToString() => $"Origin: [{Origin}] Direction: [{Direction}]";

        public bool Equals(Ray3D other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

        public override bool Equals(object obj) => obj is Ray3D other && Equals(other);

        public override int GetHashCode() => Origin.GetHashCode() ^ Direction.GetHashCode();

        public static bool operator ==(Ray3D left, Ray3D right) => left.Equals(right);

        public static bool operator !=(Ray3D left, Ray3D right) => !left.Equals(right);
    }

    /// <summary>
    /// Ray casting against triangles and axis-aligned boxes. Written for CPU picking against mesh faces.
    /// </summary>
    public static class RayIntersection
    {
        /// <summary>
        /// Degeneracy cutoff for the Möller–Trumbore determinant and for axis-parallel ray directions.
        /// Deliberately far tighter than <see cref="Tolerance.Epsilon"/>: the determinant scales with
        /// triangle area, so a 0.001 cutoff would reject legitimate hits on small faces.
        /// </summary>
        private const double ParallelEpsilon = 1e-12;

        /// <summary>
        /// Möller–Trumbore ray-triangle intersection.
        /// </summary>
        /// <param name="cullBackFaces">
        /// When true a ray striking the back of the triangle (per the A,B,C winding) misses. Mesh picking
        /// normally wants false: composite meshes in this suite are not guaranteed to be consistently wound.
        /// </param>
        /// <param name="distance">Parametric distance along the ray to the hit point. Zero on a miss.</param>
        /// <param name="u">Barycentric weight of B at the hit point. Zero on a miss.</param>
        /// <param name="v">Barycentric weight of C at the hit point. Zero on a miss.</param>
        /// <returns>True when the ray strikes the triangle at a non-negative distance.</returns>
        public static bool TryIntersectTriangle(in Ray3D ray, in Vector3 A, in Vector3 B, in Vector3 C,
            out double distance, out double u, out double v, bool cullBackFaces = false)
        {
            distance = 0;
            u = 0;
            v = 0;

            Vector3 edgeAB = B - A;
            Vector3 edgeAC = C - A;

            Vector3 pvec = Vector3.Cross(ray.Direction, edgeAC);
            double determinant = Vector3.Dot(edgeAB, pvec);

            if (cullBackFaces)
            {
                if (determinant < ParallelEpsilon)
                    return false;
            }
            else if (Math.Abs(determinant) < ParallelEpsilon)
            {
                //Ray is parallel to the triangle plane, or the triangle is degenerate.
                return false;
            }

            double inverseDeterminant = 1.0 / determinant;

            Vector3 tvec = ray.Origin - A;
            u = Vector3.Dot(tvec, pvec) * inverseDeterminant;
            if (u < 0 || u > 1)
            {
                u = 0;
                return false;
            }

            Vector3 qvec = Vector3.Cross(tvec, edgeAB);
            v = Vector3.Dot(ray.Direction, qvec) * inverseDeterminant;
            if (v < 0 || u + v > 1)
            {
                u = 0;
                v = 0;
                return false;
            }

            distance = Vector3.Dot(edgeAC, qvec) * inverseDeterminant;
            if (distance < 0)
            {
                distance = 0;
                u = 0;
                v = 0;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Möller–Trumbore ray-triangle intersection, distance only.
        /// </summary>
        public static bool TryIntersectTriangle(in Ray3D ray, in Vector3 A, in Vector3 B, in Vector3 C,
            out double distance, bool cullBackFaces = false) =>
            TryIntersectTriangle(in ray, in A, in B, in C, out distance, out _, out _, cullBackFaces);

        /// <summary>
        /// Slab-method ray intersection against an axis-aligned box.
        /// </summary>
        /// <param name="distance">
        /// Parametric distance to the entry point, or zero when the ray origin is already inside the box.
        /// Zero on a miss.
        /// </param>
        /// <returns>True when any point of the box lies at a non-negative distance along the ray.</returns>
        public static bool TryIntersectBox(in Ray3D ray, in Vector3 minCorner, in Vector3 maxCorner, out double distance)
        {
            distance = 0;

            double tEnter = double.NegativeInfinity;
            double tExit = double.PositiveInfinity;

            for (int axis = 0; axis < 3; axis++)
            {
                double origin = ray.Origin.Coords[axis];
                double direction = ray.Direction.Coords[axis];
                double min = minCorner.Coords[axis];
                double max = maxCorner.Coords[axis];

                if (Math.Abs(direction) < ParallelEpsilon)
                {
                    //Ray runs parallel to this pair of slabs, so it can only hit if it starts between them.
                    if (origin < min || origin > max)
                        return false;

                    continue;
                }

                double tMin = (min - origin) / direction;
                double tMax = (max - origin) / direction;
                if (tMin > tMax)
                    (tMin, tMax) = (tMax, tMin);

                if (tMin > tEnter)
                    tEnter = tMin;

                if (tMax < tExit)
                    tExit = tMax;

                if (tEnter > tExit)
                    return false;
            }

            if (tExit < 0)
                return false;

            distance = tEnter < 0 ? 0 : tEnter;
            return true;
        }

        /// <summary>
        /// Slab-method ray intersection against an axis-aligned box.
        /// </summary>
        public static bool TryIntersectBox(in Ray3D ray, in Box box, out double distance) =>
            TryIntersectBox(in ray, box.MinCorner, box.MaxCorner, out distance);
    }
}
