using Geometry;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using VikingXNAGraphics;
using Vector2 = Geometry.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Builds the green/magenta choice meshes from a cached triangulation plus a small patch.
    /// Distant vertices stay the cached ones. Called from the retrace command and the pen choice view.
    /// </summary>
    internal static class PolygonCutMeshBuilder
    {
        /// <summary>
        /// Flat color mesh. Triangle winding is made counter-clockwise so the overlay cull keeps the fill.
        /// </summary>
        public static PositionColorMeshModel FromIndices(PolygonCutFill fill, int[] triangleIndices, Color color)
        {
            if (fill is null)
                throw new ArgumentNullException(nameof(fill));
            if (triangleIndices is null || triangleIndices.Length < 3)
                return null;

            IReadOnlyList<Vector2> positions = fill.Positions;
            VertexPositionColor[] vertices = new VertexPositionColor[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 p = positions[i];
                vertices[i] = new VertexPositionColor(new Vector3((float)p.X, (float)p.Y, 0), color);
            }

            List<int> edges = new(triangleIndices.Length);
            for (int i = 0; i + 2 < triangleIndices.Length; i += 3)
            {
                int a = triangleIndices[i];
                int b = triangleIndices[i + 1];
                int c = triangleIndices[i + 2];
                Vector2[] tri = [positions[a], positions[b], positions[c]];
                if (tri.AreClockwise())
                {
                    edges.Add(b);
                    edges.Add(a);
                    edges.Add(c);
                }
                else
                {
                    edges.Add(a);
                    edges.Add(b);
                    edges.Add(c);
                }
            }

            if (edges.Count < 3)
                return null;

            return new PositionColorMeshModel
            {
                Vertices = vertices,
                Edges = [.. edges]
            };
        }

        /// <summary>
        /// Triangulates only <paramref name="patch"/>. Used for the cap or the growth lobe, not the rest of the cell.
        /// Returns null when the patch cannot be meshed.
        /// </summary>
        public static PositionColorMeshModel FromPatch(Polygon patch, Color color)
        {
            if (patch is null)
                return null;

            try
            {
                PolygonCutFill fill = PolygonCutFill.Create(patch);
                int[] indices = fill.TriangleIndices as int[] ?? [.. fill.TriangleIndices];
                return FromIndices(fill, indices, color);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
