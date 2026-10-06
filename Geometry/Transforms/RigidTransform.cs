using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Geometry.Transforms
{
    /// <summary>
    /// A simple translation only transform
    /// </summary>
    [Serializable]
    class RigidNoRotationTransform(Vector2 sourceToTargetOffset, TransformBasicInfo info) : IITKSerialization, ITransformBasicInfo, ISerializable, IContinuousTransform, Geometry.ITransformInfo
    {
        public TransformBasicInfo Info { get; set; } = info;

        public Vector2 SourceToTargetOffset { get; set; } = sourceToTargetOffset;

        public string GetITKTransform()
        {
            double Angle = 0;
            Vector2 CenterOfRotation = Vector2.Zero;
            var output = $"Rigid2DTransform_double_2_2 vp 3 {Angle} {SourceToTargetOffset.X} {SourceToTargetOffset.Y} fp 2 {CenterOfRotation.X} {CenterOfRotation.Y}";
            return output;
        }

        public override string ToString() => $"Rigid, Src to Tgt Offset: {SourceToTargetOffset}";

        public DateTime LastModified { get; }
        public void GetObjectData(SerializationInfo info, StreamingContext context) => throw new NotImplementedException();

        public Vector2 Transform(in Vector2 Point) => Point + SourceToTargetOffset;

        public Vector2[] Transform(in Vector2[] Points)
        {
            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = Points[i] + SourceToTargetOffset;
            return output;
        }

        public Vector2 InverseTransform(in Vector2 Point) => Point - SourceToTargetOffset;

        public Vector2[] InverseTransform(in Vector2[] Points)
        {
            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = Points[i] - SourceToTargetOffset;
            return output;
        }

        public bool CanTransform(in Vector2 Point) => true;

        public bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = Transform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public bool CanInverseTransform(in Vector2 Point) => true;

        public bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = InverseTransform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public void Translate(in Vector2 vector) => SourceToTargetOffset += vector;
    }

    /// <summary>
    /// An ITK Rigid2D transform: rotation about a center followed by a translation,
    /// <c>T(p) = R(angle) * (p - center) + center + offset</c>.
    /// </summary>
    [Serializable]
    class RigidTransform(Vector2 sourceToTargetOffset, Vector2 sourceRotationCenter, double angle, TransformBasicInfo info) : IITKSerialization, ITransformBasicInfo, ISerializable, IContinuousTransform, Geometry.ITransformInfo
    {
        public TransformBasicInfo Info { get; set; } = info;

        public Vector2 SourceToTargetOffset { get; set; } = sourceToTargetOffset;

        /// <summary>Rotation in radians, about <see cref="SourceSpaceRotationCenter"/>.</summary>
        public readonly double Angle = angle;

        public readonly Vector2 SourceSpaceRotationCenter = sourceRotationCenter;

        /// <summary>ITK text in the same layout the parser reads: <c>vp 3 angle offsetX offsetY fp 2 centerX centerY</c>.</summary>
        public string GetITKTransform()
        {
            var output = $"Rigid2DTransform_double_2_2 vp 3 {Angle} {SourceToTargetOffset.X} {SourceToTargetOffset.Y} fp 2 {SourceSpaceRotationCenter.X} {SourceSpaceRotationCenter.Y}";
            return output;
        }

        public override string ToString() => $"Rigid, Src to Tgt Offset: {SourceToTargetOffset}";

        public DateTime LastModified { get; }
        public void GetObjectData(SerializationInfo info, StreamingContext context) => throw new NotImplementedException();

        public Vector2 Transform(in Vector2 Point) =>
            (Point - SourceSpaceRotationCenter).Rotate(Angle) + SourceSpaceRotationCenter + SourceToTargetOffset;

        public Vector2[] Transform(in Vector2[] Points)
        {
            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = Transform(Points[i]);
            return output;
        }

        public Vector2 InverseTransform(in Vector2 Point) =>
            (Point - SourceToTargetOffset - SourceSpaceRotationCenter).Rotate(-Angle) + SourceSpaceRotationCenter;

        public Vector2[] InverseTransform(in Vector2[] Points)
        {
            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = InverseTransform(Points[i]);
            return output;
        }

        public bool CanTransform(in Vector2 Point) => true;

        public bool TryTransform(in Vector2 Point, out Vector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = Transform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public bool CanInverseTransform(in Vector2 Point) => true;

        public bool TryInverseTransform(in Vector2 Point, out Vector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v)
        {
            v = InverseTransform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public void Translate(in Vector2 vector) => SourceToTargetOffset += vector;
    }
}
