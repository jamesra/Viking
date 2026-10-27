using System;

namespace AnnotationVizLib
{
    /// <summary>
    /// Independent registration fixes run before a mesh is built. Combine with OR.
    /// Default is <see cref="Neighbor"/>, the residual field alone, which is all the section-correction service applies.
    /// <see cref="CurveFit"/> remains for MonogameTestbed experiments only: the service dropped it on 2026-10-27, when
    /// on 11 cells it kept the field's mean overlap of linked outlines while quadrupling the large losses
    /// (docs/publication_notes.md).
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
        /// Leave-one-out Catmull-Rom along each process, meant for large outliers the field cannot represent
        /// (fold, tear, a contour far from local consensus). Later than <see cref="Neighbor"/>. Opt-in.
        /// </summary>
        CurveFit = 2,

        /// <summary>
        /// Every fix, in order: <see cref="Neighbor"/> then <see cref="CurveFit"/>.
        /// </summary>
        All = Neighbor | CurveFit,

        /// <summary>Alias of <see cref="All"/>.</summary>
        Everything = All,
    }
}
