using System;

namespace Viking.Common
{
    /// <summary>
    /// Summary description for Class1
    /// </summary>
    public interface IProgressReporter
    {
        void ReportProgress(double progress, string message);
    }
}