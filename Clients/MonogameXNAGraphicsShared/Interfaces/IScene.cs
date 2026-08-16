
/* Unmerged change from project 'Monographics'
Before:
using System;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;
After:
using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using System;
*/
using
/* Unmerged change from project 'Monographics'
Before:
using System.Threading.Tasks;
using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
After:
using System.Threading.Tasks;
*/
Geometry;
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

        Microsoft.Xna.Framework.Graphics.Viewport Viewport { get; }
    }

    /// <summary>
    /// A 2D scene where screen coordinates can be converted directly to a point in screen coordinates
    /// </summary>
    public interface IScene2D : IScene
    {
        /// <summary>
        /// The bounds in world coordinates of the viewport
        /// </summary>
        Geometry.Rectangle VisibleWorldBounds { get; }

        Geometry.Vector2 ScreenToWorld(Geometry.Vector2 pos);

        Geometry.Vector2 ScreenToWorld(double X, double Y);

        Geometry.Vector2 WorldToScreen(Geometry.Vector2 pos);

        Geometry.Vector2 WorldToScreen(double X, double Y);
    }

    interface ICamera
    {
        Matrix View { get; }
    }
}
