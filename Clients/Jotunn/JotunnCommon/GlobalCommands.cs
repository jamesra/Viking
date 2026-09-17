using System.Windows.Input;

namespace Jotunn.Common
{
    public static class GlobalCommands
    {
        public static readonly RoutedUICommand IncrementSectionNumber = new RoutedUICommand(
            "Increment section", nameof(IncrementSectionNumber), typeof(GlobalCommands),
            [new KeyGesture(Key.PageUp)]);

        public static readonly RoutedUICommand DecrementSectionNumber = new RoutedUICommand(
            "Decrement section", nameof(DecrementSectionNumber), typeof(GlobalCommands),
            [new KeyGesture(Key.PageDown)]);

        public static readonly RoutedUICommand AddGridRowCommand = new RoutedUICommand(
            "Add grid row", nameof(AddGridRowCommand), typeof(GlobalCommands));

        public static readonly RoutedUICommand RemoveGridRowCommand = new RoutedUICommand(
            "Remove grid row", nameof(RemoveGridRowCommand), typeof(GlobalCommands));

        public static readonly RoutedUICommand AddGridColumnCommand = new RoutedUICommand(
            "Add grid column", nameof(AddGridColumnCommand), typeof(GlobalCommands));

        public static readonly RoutedUICommand RemoveGridColumnCommand = new RoutedUICommand(
            "Remove grid column", nameof(RemoveGridColumnCommand), typeof(GlobalCommands));

        public static readonly RoutedUICommand GoToLocation = new RoutedUICommand(
            "Go to location", nameof(GoToLocation), typeof(GlobalCommands),
            [new KeyGesture(Key.F12)]);

        public static readonly RoutedUICommand FindStructure = new RoutedUICommand(
            "Find structure", nameof(FindStructure), typeof(GlobalCommands),
            [new KeyGesture(Key.F11)]);

        public static readonly RoutedUICommand ContinueLast = new RoutedUICommand(
            "Continue last annotation", nameof(ContinueLast), typeof(GlobalCommands),
            [new KeyGesture(Key.F3)]);

        public static readonly RoutedUICommand DeleteAnnotation = new RoutedUICommand(
            "Delete annotation", nameof(DeleteAnnotation), typeof(GlobalCommands),
            [new KeyGesture(Key.D, ModifierKeys.Control)]);

        public static readonly RoutedUICommand HideAnnotations = new RoutedUICommand(
            "Hide annotations", nameof(HideAnnotations), typeof(GlobalCommands));

        public static readonly RoutedUICommand CommitTool = new RoutedUICommand(
            "Commit tool", nameof(CommitTool), typeof(GlobalCommands));

        public static readonly RoutedUICommand CancelTool = new RoutedUICommand(
            "Cancel tool", nameof(CancelTool), typeof(GlobalCommands));

        public static readonly RoutedUICommand PlaceCircle = new RoutedUICommand(
            "Place circle", nameof(PlaceCircle), typeof(GlobalCommands));

        public static readonly RoutedUICommand PlacePolyline = new RoutedUICommand(
            "Place polyline", nameof(PlacePolyline), typeof(GlobalCommands));

        public static readonly RoutedUICommand PlacePolygon = new RoutedUICommand(
            "Place polygon", nameof(PlacePolygon), typeof(GlobalCommands));

        public static readonly RoutedUICommand MeasureDistance = new RoutedUICommand(
            "Measure distance", nameof(MeasureDistance), typeof(GlobalCommands));

        public static readonly RoutedUICommand AnnotationPreferences = new RoutedUICommand(
            "Annotation preferences", nameof(AnnotationPreferences), typeof(GlobalCommands));

        public static readonly RoutedUICommand ViewerPreferences = new RoutedUICommand(
            "Viewer preferences", nameof(ViewerPreferences), typeof(GlobalCommands));

        public static readonly RoutedUICommand SetupChannels = new RoutedUICommand(
            "Setup channels", nameof(SetupChannels), typeof(GlobalCommands));

        public static readonly RoutedUICommand ManageStructureTypes = new RoutedUICommand(
            "Manage structure types", nameof(ManageStructureTypes), typeof(GlobalCommands));

        public static readonly RoutedUICommand Bookmarks = new RoutedUICommand(
            "Bookmarks", nameof(Bookmarks), typeof(GlobalCommands));

        public static readonly RoutedUICommand AddBookmark = new RoutedUICommand(
            "Add bookmark", nameof(AddBookmark), typeof(GlobalCommands),
            [new KeyGesture(Key.B, ModifierKeys.Control)]);

        public static readonly RoutedUICommand ExportScreenshot = new RoutedUICommand(
            "Export screenshot", nameof(ExportScreenshot), typeof(GlobalCommands));

        public static readonly RoutedUICommand ExportVisibleAnnotations = new RoutedUICommand(
            "Export visible annotations", nameof(ExportVisibleAnnotations), typeof(GlobalCommands));

        public static readonly RoutedUICommand Segmentation = new RoutedUICommand(
            "Segmentation", nameof(Segmentation), typeof(GlobalCommands));
    }
}
