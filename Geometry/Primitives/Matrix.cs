using System;
using System.Diagnostics; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 


namespace Geometry
{    
    /// <summary>
    /// Depricated class.  There are better libraries available now in C#.
    /// </summary>
    public class GridMatrix
    {
        public double[,] M;

        public override string ToString()
        {
            string Output = "";
            int[] dims = Size;
            for (int iRow = 0; iRow < dims[0]; iRow++)
            {
                for(int iCol = 0; iCol < dims[1]; iCol++)
                {
                    Output = Output + M[iRow, iCol].ToString("g4") + " ";
                }

                Output = Output + "; " + Environment.NewLine; 
            }

            return Output; 
        }

        public GridMatrix()
        {

        }

        public GridMatrix(double[,] matrix)
        {
            this.M = matrix; 
        }

        /// <summary>
        /// Solves the linear system A*X=B, returning X
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double[] LinSolve(double[,] A, double[] B)
        {
            GridMatrix matrixA = new GridMatrix(A);

            System.DateTime StartTime = System.DateTime.UtcNow;

            if (A == null || B == null)
                throw new System.ArgumentException("Null parameter passed to LinSolve"); 

            int[] dimsA = matrixA.Size;
            int dimB = B.Length; 

            int nRows = dimsA[0];
            int nCols = dimsA[1]; 

            double Epsilon = double.Epsilon * 128;
            

            if(dimsA[0] != dimsA[1])
            {
                throw new System.ArgumentException("Linear solution implementation requires a square 2D matrix"); 
            }

            if(dimsA[0] != dimB)
            {
                throw new System.ArgumentException( "Solution to matrix does not match dimensions of matrix"); 
            }

            int[] rowOrder = new int[B.Length];
            for(int i = 0; i < B.Length; i++)
            {
                rowOrder[i] = i; 
            }

            double[,] TempRow = new double[1,nCols];
            double[,] TriangleForm = new double[nRows, nCols];

            double[] ScaledResults = new double[nCols];

            Array.Copy(B, ScaledResults, B.Length); 
            Array.Copy(A, TriangleForm, A.Length); 

            for(int iRow = 0; iRow < B.Length; iRow++)
            {

                //Check if we need to swap rows to get a non-zero value in the column, require a number of a reasonable size
                if (Math.Abs(TriangleForm[iRow, iRow]) <= Epsilon)
                {
                    int iSwapRow; 
                    for(iSwapRow = iRow+1; iSwapRow < B.Length; iSwapRow++)
                    {
                        if (Math.Abs(TriangleForm[iSwapRow, iRow]) <= Epsilon)
                        {
                            continue; 
                        }

                        Array.Copy(TriangleForm, iRow * nCols, TempRow, 0, nCols);
                        Array.Copy(TriangleForm, iSwapRow * nCols, TriangleForm, iRow * nCols, nCols);
                        Array.Copy(TempRow, 0, TriangleForm, iSwapRow * nCols, nCols);

                        double tempB = ScaledResults[iRow];
                        ScaledResults[iRow] = ScaledResults[iSwapRow];
                        ScaledResults[iSwapRow] = tempB; 

                        rowOrder[iRow] = iSwapRow;
                        rowOrder[iSwapRow] = iRow;

                        break; 
                    }

                    Debug.Assert(iSwapRow != B.Length, "Multiple linear solutions to matrix");
                    if(iSwapRow >= B.Length)
                        throw new System.ArgumentException("Multiple linear solutions to matrix"); 
                }

                //Reduce our row to have a one on the diagonal
                double scalar = 1 / TriangleForm[iRow,iRow];
                TriangleForm[iRow,iRow] = 1;
                ScaledResults[iRow] = ScaledResults[iRow] * scalar;

                Parallel.For<int>(iRow + 1, nCols, () => 0,
                    (iCol, loop, t) =>
                    {
                        TriangleForm[iRow, iCol] *= scalar;
                        return t;
                    },
                 (x) => { }
                 );
                /*for (int iCol = iRow + 1; iCol < nCols; iCol++)
                {
                    TriangleForm[iRow, iCol] *= scalar;
                }
                 */

                //Reduce to echalon form
                Parallel.For<int>(iRow + 1, nRows, () => 0,
                    (iReduceRow, loop, t) =>
                    {
                        if (Math.Abs(TriangleForm[iReduceRow, iRow]) > double.Epsilon)
                        {
                            double l = -(TriangleForm[iRow, iRow] / TriangleForm[iReduceRow, iRow]);
                            TriangleForm[iReduceRow, iRow] = 0; //This is always cancelled, so just write the value without doing the math

                            ScaledResults[iReduceRow] += (ScaledResults[iRow] / l);

                            for (int iCol = iRow + 1; iCol < nCols; iCol++)
                            {
                                double val = TriangleForm[iRow, iCol] / l;
                                TriangleForm[iReduceRow, iCol] += val;
                            }
                        }

                        return iRow;
                    },
                        (x) => { }
                ); 

                /*
                for(int iReduceRow = iRow+1; iReduceRow < nRows; iReduceRow++)
                {
                    if (Math.Abs(TriangleForm[iReduceRow, iRow]) > double.Epsilon)
                    {
                        double l = -(TriangleForm[iRow, iRow] / TriangleForm[iReduceRow, iRow]);
                        TriangleForm[iReduceRow, iRow] = 0; //This is always cancelled, so just write the value without doing the math

                        ScaledResults[iReduceRow] += (ScaledResults[iRow] / l);

                        for (int iCol = iRow+1; iCol < nCols; iCol++)
                        {
                            double val =  TriangleForm[iRow, iCol] / l;
                            TriangleForm[iReduceRow, iCol] += val;
                        }
                    }
                }
                */
            }

            double[,] ReducedForm = new double[nRows, nCols];
            Array.Copy(TriangleForm, ReducedForm, TriangleForm.Length);

            double[] Weights = new double[nRows];
            Array.Copy(ScaledResults, Weights, ScaledResults.Length);

            //OK, now solve the system
            for (int iRow = nRows - 1; iRow >= 0; iRow--)
            {
                double Sum = 0; 

                for (int iCol = nCols - 1; iCol > iRow; iCol--)
                {
                    Sum += ReducedForm[iRow, iCol] * Weights[iCol]; 
                }

                Weights[iRow] = Weights[iRow] - Sum; 
            }

            //Reorder the weights correctly
            Double[] FinalWeights = new Double[nRows];
            for (int iRow = 0; iRow < nRows; iRow++)
            {
                FinalWeights[iRow] = Weights[rowOrder[iRow]]; 
            }

            System.DateTime EndTime = System.DateTime.UtcNow;

            System.TimeSpan Elapsed = new TimeSpan(EndTime.Ticks - StartTime.Ticks);

            Trace.WriteLine("Matrix solution completed, elapsed: " + Elapsed.TotalSeconds.ToString("G4"));

            return FinalWeights; 
        }

        public int[] Size
        {
            get
            {
                
                int rank = M.Rank;
                int[] dims = new int[rank];
                for (int iDim = 0; iDim < rank; iDim++)
                {
                    dims[iDim] = M.GetUpperBound(iDim)+1; 
                }

                return dims; 
            }
        }

    }
}
