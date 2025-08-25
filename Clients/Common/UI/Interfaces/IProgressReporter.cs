namespace Viking.Common
{
    public struct ProgressInfo
    { 
        public string Message { get; }
        public double Progress { get; }
        public double MaxProgress { get; }

        public ProgressInfo(string message, double progress, double maxProgress = 100)
        {
            Message = message;
            Progress = progress;
            MaxProgress = maxProgress;
        }
    }

    public interface IProgressReporter : System.IProgress<ProgressInfo>
    {
        void Report(string message, double progress, double maxProgress);
    }
}
