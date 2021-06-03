namespace Viking.Common
{
    public interface IProgressReporter
    {
        void ReportProgress(double PercentProgress, string message);

        void TaskComplete();
    }
}
