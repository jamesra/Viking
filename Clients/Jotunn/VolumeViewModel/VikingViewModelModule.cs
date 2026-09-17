using Viking.Common;
using System.ComponentModel;

namespace Viking.VolumeViewModel
{
    public class BackgroundThreadProgressReporter : IProgressReporter
    {
        readonly BackgroundWorker worker;

        public BackgroundThreadProgressReporter(BackgroundWorker worker)
        {
            this.worker = worker;
        }

        public void Report(string message, double progress, double maxProgress)
        {
            int percent = (int)((progress / maxProgress) * 100);
            worker.ReportProgress(percent, message);
        }

        public void Report(ProgressInfo info)
        {
            int percent = info.MaxProgress > 0
                ? (int)((info.Progress / info.MaxProgress) * 100)
                : (int)info.Progress;
            worker.ReportProgress(percent, info.Message);
        }

        public void ReportProgress(double PercentProgress, string message)
        {
            worker.ReportProgress((int)PercentProgress, message);
        }

        public void TaskComplete()
        {
            worker.ReportProgress(100, "Task complete");
        }
    }
}
