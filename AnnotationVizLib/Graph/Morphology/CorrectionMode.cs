using System;

namespace AnnotationVizLib
{
    /// <summary>
    /// Independent registration fixes run before a mesh is built. Combine with OR.
    /// Default is <see cref="All"/>: residual field, then outlier curvefit.
    /// </summary>
    [Flags]
    public enum CorrectionMode
    {
        None = 0,

        /// <summary>
        /// Spatial mean displacement from neighboring process curve-residuals.
        /// Corrects smoothly varying leftover registration after Stos. Does not run curvefit.
        /// </summary>
        Neighbor = 1,

        /// <summary>
        /// Leave-one-out Catmull-Rom for large outliers the field cannot represent
        /// (fold, tear, a contour far from local consensus). Later than <see cref="Neighbor"/>.
        /// </summary>
        CurveFit = 2,

        /// <summary>
        /// Every fix, in order: <see cref="Neighbor"/> then <see cref="CurveFit"/>. CLI default.
        /// </summary>
        All = Neighbor | CurveFit,

        /// <summary>Alias of <see cref="All"/>.</summary>
        Everything = All,
    }
}
