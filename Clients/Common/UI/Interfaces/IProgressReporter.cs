namespace Viking.Common
{
    public readonly struct ProgressInfo(string message, double progress, double maxProgress = 100)
    {
        public string Message { get; } = message;
        public double Progress { get; } = progress;
        public double MaxProgress { get; } = maxProgress;
    }

    public interface IProgressReporter : System.IProgress<ProgressInfo>
    {
        void Report(string message, double progress, double maxProgress);
    }
}
