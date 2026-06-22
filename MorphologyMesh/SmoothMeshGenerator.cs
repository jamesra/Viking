using Geometry.Meshing;

namespace MorphologyMesh
{
    /// <summary>
    /// Stub for smooth mesh generation from MeshGraph. Returns an empty mesh so call sites compile.
    /// Full implementation was removed or moved; replace with actual generator when available.
    /// </summary>
    public static class SmoothMeshGenerator
    {
        public static Mesh3D<IVertex3D<ulong>> Generate(MeshGraph graph)
        {
            return new Mesh3D<IVertex3D<ulong>>();
        }
    }
}
