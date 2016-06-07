using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.IO;

namespace Geometry.Transforms
{
    [Serializable]
    public abstract class ReferencePointBasedTransform  : TransformBase, IITKSerialization
    {
        private GridRectangle _ControlBounds = new GridRectangle();
        public GridRectangle ControlBounds
        {
            get
            {
                if (_ControlBounds.Width <= 0)
                {
                    _ControlBounds = MappingGridVector2.CalculateControlBounds(this.MapPoints); 
                }

                return _ControlBounds; 
            }
            protected set
            {
                _ControlBounds = value;
            }
        }

        private GridRectangle _MappedBounds = new GridRectangle();
        public GridRectangle MappedBounds
        {
            get
            {
                if (_MappedBounds.Width <= 0)
                {
                    _MappedBounds = MappingGridVector2.CalculateMappedBounds(this.MapPoints);
                }

                return _MappedBounds;
            }
            protected set
            {
                _MappedBounds = value;
            }
        }

        /// <summary>
        /// List of points that define transform.  Triangles are derived from these points.  They should be populated at creation.  They may
        /// be replaced during a transformation with a new list, which requires regenerating triangles and any other derived data.
        /// These points are sorted by control point x, lowest to highest
        /// </summary>
        private MappingGridVector2[] _mapPoints = new MappingGridVector2[0];
        public MappingGridVector2[] MapPoints
        {
            get { return _mapPoints; }
            protected set
            {
                //SortedSet<MappingGridVector2> listPoints = new SortedSet<MappingGridVector2>(value);
                Array.Sort(value);
                _mapPoints = value; 
#if DEBUG
                DebugVerifyPointsAreUnique(_mapPoints); 
#endif
                 
                //Reset the bounds
                MappedBounds = new GridRectangle();
                ControlBounds = new GridRectangle(); 
            }
        }

        private static void DebugVerifyPointsAreUnique(MappingGridVector2[] listPoints)
        {
            //Check for duplicate points
            for (int i = 1; i < listPoints.Length; i++)
            {
                Debug.Assert(listPoints[i - 1].ControlPoint != listPoints[i].ControlPoint, "Duplicate Points found in transform.  This breaks Delaunay.");
                Debug.Assert(listPoints[i - 1].MappedPoint != listPoints[i].MappedPoint, "Duplicate Points found in transform.  This breaks Delaunay.");
            } 
        }

        protected ReferencePointBasedTransform(MappingGridVector2[] points, TransformInfo info)
            : base(info)
        {
            //List<MappingGridVector2> listPoints = new List<MappingGridVector2>(points);
            //MappingGridVector2.RemoveDuplicates(listPoints);

            //this.MapPoints = listPoints.ToArray();
            this.MapPoints = points;
        }

        protected ReferencePointBasedTransform(MappingGridVector2[] points, GridRectangle mappedBounds, TransformInfo info)
            : this(points, info)
        { 
            this.MappedBounds = mappedBounds;
        }

        protected ReferencePointBasedTransform(MappingGridVector2[] points, GridRectangle mappedBounds, GridRectangle controlBounds, TransformInfo info)
            : base(info)
        {
            this.MapPoints = points;
            this.MappedBounds = mappedBounds;
            this.ControlBounds = controlBounds;
        }

        protected ReferencePointBasedTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException(); 

            _mapPoints = info.GetValue("_mapPoints", typeof(MappingGridVector2[])) as MappingGridVector2[];
            MappedBounds = (GridRectangle)info.GetValue("MappedBounds", typeof(GridRectangle));
            ControlBounds = (GridRectangle)info.GetValue("ControlBounds", typeof(GridRectangle));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(); 

            info.AddValue("_mapPoints", _mapPoints);
            info.AddValue("MappedBounds", MappedBounds);
            info.AddValue("ControlBounds", ControlBounds); 

            base.GetObjectData(info, context);
        }

        /// <summary>
        /// Translates all verticies in the tile according to the vector
        /// </summary>
        /// <param name="vector"></param>
        public override void Translate(GridVector2 vector)
        {
            for (int i = 0; i < MapPoints.Length; i++)
            {
                MapPoints[i].ControlPoint += vector;
            }

            //Remove any cached data structures
            //MinimizeMemory();

            ControlBounds = new GridRectangle(ControlBounds.Left + vector.X,
                                              ControlBounds.Right + vector.X,
                                              ControlBounds.Bottom + vector.Y,
                                              ControlBounds.Top + vector.Y);
        }

        public static GridRectangle CalculateControlBounds(ReferencePointBasedTransform[] transforms)
        {
            if (transforms == null || transforms.Length == 0)
                return new GridRectangle();

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (ReferencePointBasedTransform T in transforms)
            {
                GridRectangle R = T.ControlBounds;

                if (R.Left < minX)
                    minX = R.Left;
                if (R.Right > maxX)
                    maxX = R.Right;
                if (R.Bottom < minY)
                    minY = R.Bottom;
                if (R.Top > maxY)
                    maxY = R.Top;
            }            

            return new GridRectangle(minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Save a transform using the itk transform text format
        /// </summary>
        /// <param name="stream"></param>
        public void WriteITKTransform(StreamWriter stream)
        { 
            if (stream == null)
                throw new ArgumentNullException("stream");

            double Downsample = 1.0;

            StringBuilder output = new StringBuilder();
            string transform = "meshtransform_double_2_2";

            output.Append("0\n0\n");
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", ControlBounds.Left, ControlBounds.Bottom, ControlBounds.Right, ControlBounds.Top);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", MappedBounds.Left, MappedBounds.Bottom, MappedBounds.Right, MappedBounds.Top);
            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}\n", 0, 0, ControlBounds.Width / Downsample, ControlBounds.Height / Downsample);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", 0, 0, MappedBounds.Width, MappedBounds.Height);

            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}\n", MappedBounds.Left / Downsample, MappedBounds.Bottom / Downsample, MappedBounds.Width / Downsample, MappedBounds.Height / Downsample);

            output.Append(transform + " vp ");
            output.AppendFormat("{0:d}", this.MapPoints.Length * 4);

            foreach (MappingGridVector2 p in this.MapPoints)
            {
                output.AppendFormat(" {0:g} {1:g} {2:g} {3:g}",
                                        (p.MappedPoint.X - MappedBounds.Left) / MappedBounds.Width,
                                        (p.MappedPoint.Y - MappedBounds.Bottom) / MappedBounds.Height,
                                        (p.ControlPoint.X) / Downsample,
                                        (p.ControlPoint.Y) / Downsample);
            }

            output.Append(" fp 8 0 0 0 ");
            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}", MappedBounds.Left / Downsample, MappedBounds.Bottom / Downsample, MappedBounds.Width / Downsample, MappedBounds.Height / Downsample);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}", 0,0, MappedBounds.Width, MappedBounds.Height);

            output.AppendFormat(" {0:d}\n", this.MapPoints.Length);

            stream.Write(output.ToString());
        } 
    }
}
