using System;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    public static class Smoothing
    {
        /// <summary>
        /// Smooth the X/Y Values while leaving the endpoints fixed
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static GridVector2[] Gaussian(GridVector2[] points)
        {
            double[] kernel = { 0.25, 0.5, 0.25 };
            Debug.Assert(kernel.Sum() == 1.0, "Kernel values should sum to 1");
            Debug.Assert((kernel.Length % 2) == 1, "Kernel should have an odd number of values");

            int kernelRadius = (kernel.Length - 1) / 2;

            GridVector2[] output = new GridVector2[points.Length];

            //Copy the values we aren't smoothing
            for (int i = 0; i < kernelRadius; i++)
            {
                output[i] = points[i];
            }

            for (int i = points.Length - kernelRadius; i < points.Length; i++)
            {
                output[i] = points[i];
            }

            //Smooth the points using the adjacent positions
            for (int i = kernelRadius; i < points.Length - kernelRadius; i++)
            {
                output[i] = ApplyKernelToIndex(points, kernel, i);
            }

            return output;
        }

        private static GridVector2 ApplyKernelToIndex(GridVector2[] points, double[] kernel, int iCenter)
        {
            int kernelRadius = (kernel.Length - 1) / 2;
            GridVector2[] items = GetKernelInput(points, kernelRadius, iCenter);

            double X = items.Select((p, i) => p.X * kernel[i]).Sum();
            double Y = items.Select((p, i) => p.Y * kernel[i]).Sum();

            return new GridVector2(X, Y);
        }

        /// <summary>
        /// Return the points under the kernel.  Kernel must have an odd number of values
        /// </summary>
        /// <param name="points"></param>
        /// <param name="kernelRadius">The number of items adjacent to the center that should be fetched. </param>
        /// <param name="iCenter">Center item the kernel is applied to</param>
        /// <returns></returns>
        private static GridVector2[] GetKernelInput(this GridVector2[] points, int kernelRadius, int iCenter)
        {
            int KernelSize = (kernelRadius * 2) + 1;
            GridVector2[] output = new GridVector2[KernelSize];

            Array.Copy(points, iCenter - kernelRadius, output, 0, KernelSize);
            return output;
        }

    }
}
