using System;
using Viking.Common;

namespace Viking.UI.Forms
{
    public class ProgressReporter : IProgressReporter
    {
        private readonly IProgress<ProgressInfo> _progress;

        public ProgressReporter(Action<ProgressInfo> progress_action)
        {
            if (progress_action is null)
                throw new ArgumentNullException(nameof(progress_action));

            _progress = new Progress<ProgressInfo>(progress_action);
        }

        public ProgressReporter(Progress<ProgressInfo> progress)
        {
            _progress = progress as IProgress<ProgressInfo>;
        }

        public void Report(ProgressInfo info) => _progress.Report(info);

        public void Report(string message, double progress, double maxProgress) => _progress.Report(new ProgressInfo(message, progress, maxProgress));

        public void TaskComplete() => _progress.Report(new ProgressInfo("Task complete!", 100, 100));
    }
}