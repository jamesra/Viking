using System.Windows.Input;

namespace Jotunn.Common
{
    public static class GlobalCommands
    {
        public static readonly RoutedUICommand IncrementSectionNumber = new RoutedUICommand(
            "Increment section", nameof(IncrementSectionNumber), typeof(GlobalCommands));

        public static readonly RoutedUICommand DecrementSectionNumber = new RoutedUICommand(
            "Decrement section", nameof(DecrementSectionNumber), typeof(GlobalCommands));

        public static readonly RoutedUICommand AddGridRowCommand = new RoutedUICommand(
            "Add grid row", nameof(AddGridRowCommand), typeof(GlobalCommands));

        public static readonly RoutedUICommand RemoveGridRowCommand = new RoutedUICommand(
            "Remove grid row", nameof(RemoveGridRowCommand), typeof(GlobalCommands));

        public static readonly RoutedUICommand AddGridColumnCommand = new RoutedUICommand(
            "Add grid column", nameof(AddGridColumnCommand), typeof(GlobalCommands));

        public static readonly RoutedUICommand RemoveGridColumnCommand = new RoutedUICommand(
            "Remove grid column", nameof(RemoveGridColumnCommand), typeof(GlobalCommands));
    }
}
