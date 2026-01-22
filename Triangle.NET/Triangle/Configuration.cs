// -----------------------------------------------------------------------
// <copyright file="Configuration.cs" company="">
// Triangle.NET code by Christian Woltering, http://triangle.codeplex.com/
// </copyright>
// -----------------------------------------------------------------------

namespace TriangleNet
{
    using System;

    /// <summary>
    /// Configure advanced aspects of the library.
    /// </summary>
    public class Configuration(Func<IPredicates> predicates, Func<TrianglePool> trianglePool)
    {
        public Configuration()
            : this(() => RobustPredicates.Default, () => [])
        {
        }

        public Configuration(Func<IPredicates> predicates)
            : this(predicates, () => [])
        {
        }

        /// <summary>
        /// Gets or sets the factory method for the <see cref="IPredicates"/> implementation.
        /// </summary>
        public Func<IPredicates> Predicates { get; set; } = predicates;

        /// <summary>
        /// Gets or sets the factory method for the <see cref="TrianglePool"/>.
        /// </summary>
        public Func<TrianglePool> TrianglePool { get; set; } = trianglePool;
    }
}
