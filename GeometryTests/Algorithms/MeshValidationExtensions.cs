using FsCheck;
using Geometry;
using Geometry.Meshing;
using System.Linq;

namespace GeometryTests.Algorithms
{

    public static class MeshValidationExtensions
    {

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreTriangulatedVertexEdgesValid(this TriangulationMesh<IVertex2D> mesh)
        {
            foreach (var v in mesh.Verticies)
            {
                //Assert.IsTrue(v.Edges.Count > 1); //Every vertex must have at least two edges
                if (v.Edges.Count <= 1)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreTriangulatedFacesCCW(this IReadOnlyMesh2D<IVertex2D> mesh)
        {
            foreach (Face f in mesh.Faces)
            {
                bool IsClockwise = mesh.IsClockwise(f);
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (IsClockwise)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreTriangulatedFacesColinear(this IReadOnlyMesh2D<IVertex2D> mesh)
        {
            foreach (Face f in mesh.Faces)
            {
                RotationDirection winding = mesh.Winding(f);
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (winding == RotationDirection.COLINEAR)
                    return true;

                if (f.iVerts.Count() == 3)
                {
                    GridTriangle tri = new GridTriangle(mesh[f.iVerts].Select(v => v.Position).ToArray());
                    if (tri.Area == 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static bool AreFacesTriangles(this IReadOnlyMesh2D<IVertex2D> mesh)
        {
            foreach (IFace f in mesh.Faces)
            {
                bool IsTriangle = f.iVerts.Length == 3;
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (!IsTriangle)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check that all verticies have at least two edges. 
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Property AreTriangulatedFacesDelaunay(this TriangulationMesh<IVertex2D> mesh, out bool result)
        {
            result = true;
            foreach (Face f in mesh.Faces)
            {
                bool IsDelaunay = mesh.IsTriangleDelaunay(f);
                //Assert.IsTrue(IsDelaunay, string.Format("{0} is not a delaunay triangle", f));
                //Assert.IsFalse(IsClockwise, string.Format("{0} is clockwise, incorrect winding.", f));

                if (!IsDelaunay)
                {
                    result = false;
                    return false.Label(string.Format("Face {0} is not delaunay", f));
                }
            }

            return true.ToProperty();
        }


    }
}
