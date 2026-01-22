// -----------------------------------------------------------------------
// <copyright file="Edge.cs" company="">
// Triangle.NET code by Christian Woltering, http://triangle.codeplex.com/
// </copyright>
// -----------------------------------------------------------------------

namespace TriangleNet.Geometry
{
    /// <summary>
    /// Represents a straight line segment in 2D space.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Edge" /> class.
    /// </remarks>
    public class Edge(int p0, int p1, int label) : IEdge
    {
        /// <summary>
        /// Gets the first endpoints index.
        /// </summary>
        public int P0
        {
            get;
            private set;
        } = p0;

        /// <summary>
        /// Gets the second endpoints index.
        /// </summary>
        public int P1
        {
            get;
            private set;
        } = p1;

        /// <summary>
        /// Gets the segments boundary mark.
        /// </summary>
        public int Label
        {
            get;
            private set;
        } = label;

        /// <summary>
        /// Initializes a new instance of the <see cref="Edge" /> class.
        /// </summary>
        public Edge(int p0, int p1)
            : this(p0, p1, 0)
        { }
    }
}
