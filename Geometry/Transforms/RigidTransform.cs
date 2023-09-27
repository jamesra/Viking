using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using MathNet.Numerics.LinearAlgebra;

namespace Geometry.Transforms
{
    /// <summary>
    /// A simple translation only transform
    /// </summary>
    [Serializable]
    class RigidNoRotationTransform : IITKSerialization, ITransformBasicInfo, ISerializable, IContinuousTransform, Geometry.ITransformInfo
    {
        public TransformBasicInfo Info { get; set; }

        public GridVector2 SourceToTargetOffset { get; set; }

        public RigidNoRotationTransform(GridVector2 sourceToTargetOffset, TransformBasicInfo info)
        {
            SourceToTargetOffset = sourceToTargetOffset;
            Info = info;
        }

        public string ITKTransformString()
        {
            double Angle = 0;
            GridVector2 CenterOfRotation = GridVector2.Zero;
            var output = $"Rigid2DTransform_double_2_2 vp 3 {Angle} {SourceToTargetOffset.X} {SourceToTargetOffset.Y} fp 2 {CenterOfRotation.X} {CenterOfRotation.Y}";
            return output;
        }

        public void WriteITKTransform(StreamWriter stream)
        {
            stream.Write(ITKTransformString());
        }

        public override string ToString()
        {
            return $"Rigid, Src to Tgt Offset: {SourceToTargetOffset}";
        }

        public DateTime LastModified { get; }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }

        public GridVector2 Transform(in GridVector2 Point)
        {
            return Point + SourceToTargetOffset;
        }

        public GridVector2[] Transform(in GridVector2[] Points)
        {
            GridVector2[] output = new GridVector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = Points[i] + SourceToTargetOffset;
            return output;
        }

        public GridVector2 InverseTransform(in GridVector2 Point)
        {
            return Point - SourceToTargetOffset;
        }

        public GridVector2[] InverseTransform(in GridVector2[] Points)
        {
            GridVector2[] output = new GridVector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = Points[i] - SourceToTargetOffset;
            return output;
        }

        public bool CanTransform(in GridVector2 Point)
        {
            return true;
        }

        public bool TryTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(in GridVector2[] Points, out GridVector2[] v)
        {
            v = Transform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public bool CanInverseTransform(in GridVector2 Point)
        {
            return true;
        }

        public bool TryInverseTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(in GridVector2[] Points, out GridVector2[] v)
        {
            v = InverseTransform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public void Translate(in GridVector2 vector)
        {
            SourceToTargetOffset += vector;
        }
    }

    /// <summary>
    /// A simple translation only transform
    /// </summary>
    [Serializable]
    class RigidTransform : IITKSerialization, ITransformBasicInfo, ISerializable, IContinuousTransform, Geometry.ITransformInfo
    {
        public TransformBasicInfo Info { get; set; }

        public GridVector2 SourceToTargetOffset { get; set; }

        public readonly double Angle;
        
        public readonly GridVector2 SourceSpaceRotationCenter;
         
        public RigidTransform(GridVector2 sourceToTargetOffset, GridVector2 sourceRotationCenter, double angle, TransformBasicInfo info)
        {
            SourceToTargetOffset = sourceToTargetOffset;
            Angle = angle;
            SourceSpaceRotationCenter = sourceRotationCenter;
             

            Info = info;
        }
         

        public string ITKTransformString()
        {
            double Angle = 0;
            GridVector2 CenterOfRotation = GridVector2.Zero;
            var output = $"Rigid2DTransform_double_2_2 vp 3 {Angle} {SourceToTargetOffset.X} {SourceToTargetOffset.Y} fp 2 {SourceSpaceRotationCenter.X} {SourceSpaceRotationCenter.Y}";
            return output;
        }

        public void WriteITKTransform(StreamWriter stream)
        {
            stream.Write(ITKTransformString());
        }

        public override string ToString()
        {
            return $"Rigid, Src to Tgt Offset: {SourceToTargetOffset}";
        }

        public DateTime LastModified { get; }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }

        public GridVector2 Transform(in GridVector2 Point)
        {
            return Transform(new GridVector2[] {Point})[0];
        }

        public GridVector2[] Transform(in GridVector2[] Points)
        {            
            var rotated_points = Points.Rotate(this.Angle, this.SourceSpaceRotationCenter);
            rotated_points.Translate(this.SourceToTargetOffset);
            return rotated_points;
        }

        public GridVector2 InverseTransform(in GridVector2 Point)
        {
            return InverseTransform(new GridVector2[] { Point })[0];
        }

        public GridVector2[] InverseTransform(in GridVector2[] Points)
        {
            var translated_points = Points.Translate(-this.SourceToTargetOffset);
            var rotated_points = Points.Rotate(-this.Angle, this.SourceSpaceRotationCenter); 
            return rotated_points;
        }

        public bool CanTransform(in GridVector2 Point)
        {
            return true;
        }

        public bool TryTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = Transform(Point);
            return true;
        }

        public bool[] TryTransform(in GridVector2[] Points, out GridVector2[] v)
        {
            v = Transform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public bool CanInverseTransform(in GridVector2 Point)
        {
            return true;
        }

        public bool TryInverseTransform(in GridVector2 Point, out GridVector2 v)
        {
            v = InverseTransform(Point);
            return true;
        }

        public bool[] TryInverseTransform(in GridVector2[] Points, out GridVector2[] v)
        {
            v = InverseTransform(Points);
            var output = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
                output[i] = true;

            return output;
        }

        public void Translate(in GridVector2 vector)
        {
            SourceToTargetOffset += vector;
        }
    }
}
