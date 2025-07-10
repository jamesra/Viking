using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNA
{
    public interface IScene
    {
        Matrix Projection { get; }
        Matrix World { get; }
        Matrix View { get; }
        Matrix ViewProj { get; }
        Matrix WorldViewProj { get; }
        Viewport Viewport { get; }
    }

    public interface IScene2D : IScene
    {
        GridRectangle VisibleWorldBounds { get; }
        GridVector2 ScreenToWorld(GridVector2 pos);
        GridVector2 ScreenToWorld(double X, double Y);
        GridVector2 WorldToScreen(GridVector2 pos);
        GridVector2 WorldToScreen(double X, double Y);
    }

    public interface ICamera
    {
        Matrix View { get; }
    }
} 