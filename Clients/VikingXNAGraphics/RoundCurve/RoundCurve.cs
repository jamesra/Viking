using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoundCurve
{
    public enum HorizontalAlignment
    {
        Center,
        Left,
        Right
    }

    public partial class RoundCurve
    {
        private GridVector2[] _controlPoints;
        private double[] _tangent_thetas;
        private double[] _distance_to_origin;
        private double[] _distance_to_origin_normalized;
        private readonly bool _Closed;

        public GridVector2[] ControlPoints
        {
            get => _controlPoints;
            set
            {
                _controlPoints = value;
                RecalcDistanceAndTheta();
            }
        }

        public bool Closed => _Closed;
        public double[] Distance => _distance_to_origin;
        public double[] DistanceNormalized => _distance_to_origin_normalized;
        public double[] Theta => _tangent_thetas;
        public double TotalDistance => _distance_to_origin.Last();

        public RoundCurve(GridVector2[] ControlPoints, bool Closed)
        {
            this._Closed = Closed;
            this.ControlPoints = ControlPoints;
        }

        private static double[] CalcLineDistances(GridVector2[] points)
        {
            double total_distance = 0;
            double[] point_distances = new double[points.Length];
            point_distances[0] = 0;

            for (int i = 1; i < points.Length; i++)
            {
                double step_distance = GridVector2.Distance(points[i], points[i - 1]);
                total_distance += step_distance;
                point_distances[i] = total_distance;
            }

            return point_distances;
        }

        private static double[] CalcLineTangents(GridVector2[] points, bool Closed)
        {
            double[] tangents = new double[points.Length];
            int numPoints = points.Length;

            for (int i = 1; i < numPoints - 1; i++)
            {
                tangents[i] = GridVector2.Angle(points[i - 1], points[i + 1]);
            }

            if (Closed)
            {
                tangents[0] = GridVector2.Angle(points[numPoints - 2], points[1]);
                tangents[numPoints - 1] = GridVector2.Angle(points[numPoints - 2], points[1]);
            }
            else
            {
                tangents[0] = GridVector2.Angle(points[0], points[1]);
                tangents[numPoints - 1] = GridVector2.Angle(points[numPoints - 2], points[numPoints - 1]);
            }

            return tangents;
        }

        protected void RecalcDistanceAndTheta()
        {
            this._distance_to_origin = CalcLineDistances(this._controlPoints);
            double TotalDistance = _distance_to_origin.Last();
            this._distance_to_origin_normalized = _distance_to_origin.Select(d => d / TotalDistance).ToArray();
            this._tangent_thetas = CalcLineTangents(this._controlPoints, this._Closed);
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", _controlPoints[0], _controlPoints.Last());
        }
    }

    public class CurveManager : VikingXNAGraphics.IInitEffect
    {
        protected GraphicsDevice device;
        protected Effect effect;

        public virtual void Init(GraphicsDevice device, ContentManager content)
        {
            this.device = device;
            // Placeholder - would load actual effect
        }

        public virtual void Draw(RoundCurve roundLine, float lineRadius, Color lineColor, Matrix viewProjMatrix, float time, string techniqueName)
        {
            // Placeholder implementation
        }
    }

    public class CurveManagerHSV : CurveManager
    {
        public Texture LumaTexture { get; set; }
        public Viewport RenderTargetSize { get; set; }

        public override void Init(GraphicsDevice device, ContentManager content)
        {
            base.Init(device, content);
            // Placeholder - would load actual effect
        }
    }
} 