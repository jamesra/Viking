// -----------------------------------------------------------------------
// <copyright file="InputTriangle.cs" company="">
// Triangle.NET code by Christian Woltering, http://triangle.codeplex.com/
// </copyright>
// -----------------------------------------------------------------------

namespace TriangleNet.IO
{
    using TriangleNet.Geometry;

    /// <summary>
    /// Simple triangle class for input.
    /// </summary>
    public class InputTriangle(int p0, int p1, int p2) : ITriangle
    {
        internal int[] vertices = [p0, p1, p2];
        internal int label;
        internal double area;

        #region Public properties

        /// <summary>
        /// Gets the triangle id.
        /// </summary>
        public int ID
        {
            get => 0;
            set { }
        }

        /// <summary>
        /// Region ID the triangle belongs to.
        /// </summary>
        public int Label
        {
            get => label;
            set => label = value;
        }

        /// <summary>
        /// Gets the triangle area constraint.
        /// </summary>
        public double Area
        {
            get => area;
            set => area = value;
        }

        /// <summary>
        /// Gets the specified corners vertex.
        /// </summary>
        public Vertex GetVertex(int index) => null; // TODO: throw NotSupportedException?

        public int GetVertexID(int index) => vertices[index];

        public ITriangle GetNeighbor(int index) => null;

        public int GetNeighborID(int index) => -1;

        public ISegment GetSegment(int index) => null;

        #endregion
    }
}
