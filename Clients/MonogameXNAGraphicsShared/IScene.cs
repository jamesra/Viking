using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework;

namespace VikingXNA
{
    public interface IScene
    {
        Matrix Projection { get; }
        Matrix World { get; }
        Matrix View { get; }

        Matrix ViewProj { get; }

        Matrix WorldViewProj { get; }
    }

    /// <summary>
    /// A 2D scene where screen coordinates can be converted directly to a point in screen coordinates
    /// </summary>
    public interface IScene2D : IScene
    {
        /// <summary>
        /// The bounds in world coordinates of the viewport
        /// </summary>
        Geometry.GridRectangle VisibleWorldBounds { get; }

        GridVector2 ScreenToWorld(GridVector2 pos);

        GridVector2 ScreenToWorld(double X, double Y);

        GridVector2 WorldToScreen(GridVector2 pos);

        GridVector2 WorldToScreen(double X, double Y);
    }

    interface ICamera
    {
        Matrix View { get; }
    }
}
