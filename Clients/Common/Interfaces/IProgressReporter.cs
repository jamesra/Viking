using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.Common
{
    public interface IProgressReporter
    {
        void ReportProgress(double PercentProgress, string message);

        void TaskComplete();
    }
}
