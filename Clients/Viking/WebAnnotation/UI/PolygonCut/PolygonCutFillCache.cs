using Geometry;
using Geometry.Meshing;
using System;
using System.Threading.Tasks;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// One background triangulation per polygon instance. The polygon view starts it when the cell
    /// is shown, and the retrace command starts it if that view has not, so the choice fill does
    /// not triangulate the whole ring on the pen thread.
    /// </summary>
    internal static class PolygonCutFillCache
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Polygon, Task<PolygonCutFill>> Tasks = new();

        /// <summary>
        /// Returns the in-flight or finished triangulation for this polygon instance.
        /// Clones before the task so a later edit of the live ring does not change the mesh.
        /// </summary>
        public static Task<PolygonCutFill> Begin(Polygon polygon)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));

            if (Tasks.TryGetValue(polygon, out Task<PolygonCutFill> existing))
                return existing;

            Polygon copy = (Polygon)polygon.Clone();
            return Tasks.GetValue(polygon, _ => Task.Run(() => PolygonCutFill.Create(copy)));
        }
    }
}
