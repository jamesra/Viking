namespace MonogameTestbed
{
    /// <summary>
    /// Optional View-menu hooks for tests that expose slice-status overlays.
    /// </summary>
    interface IViewMenuTarget
    {
        bool ShowInProgressSliceStatus { get; set; }

        bool ShowSectionReadySliceStatus { get; set; }

        bool ShowMinorIssueSliceStatus { get; set; }

        bool ShowWarningSliceStatus { get; set; }

        bool ShowCriticalSliceStatus { get; set; }
    }
}
