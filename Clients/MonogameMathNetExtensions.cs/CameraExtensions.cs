using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNAGraphics;
using VikingXNA;
using MathNet.Numerics.LinearAlgebra;

namespace VikingXNAGraphics
{
    public static class CameraExtensions
    {

        /// <summary>
        /// Translate the provided difference vector according to the current view direction of the camera
        /// </summary>
        /// <param name="X">Left/Right Yaw</param>
        /// <param name="Y">Up/Down Pitch</param>
        /// <param name="Z">In/Out of screen</param>
        /// <returns></returns>
        public static Microsoft.Xna.Framework.Vector3 TranslateRelativeToViewMatrix(this Microsoft.Xna.Framework.Matrix XNAView , double X, double Y, double Z)
        {
            Vector<double> oDelta = Vector<double>.Build.DenseOfArray(new double[] { X, Y, Z, 1.0 });
            Matrix<double> view = XNAView.ToMathnetMatrix();
            Vector<double> tDelta = view * oDelta;

            Microsoft.Xna.Framework.Vector3 translation = tDelta.ToXNAVector3();
            return translation;
        }
    }
}
