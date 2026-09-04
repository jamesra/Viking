using System.Collections.Generic;

namespace MonogameTestbed
{
    /// <summary>
    /// Builds the Help dialog content: global mode-switch keys plus any bindings the active test declares.
    /// </summary>
    static class TestHotkeyRegistry
    {
        public static IReadOnlyList<HotkeyBinding> GlobalBindings { get; } =
        [
            new("F1", "Switch to Curve test"),
            new("F2", "Switch to Curve Label test"),
            new("F3", "Switch to Text / Labels test"),
            new("F4", "Switch to Line Styles test"),
            new("F5", "Switch to Curve Styles test"),
            new("F6", "Switch to Closed Curve test"),
            new("F7", "Switch to Polygon 2D test"),
            new("F8", "Switch to Mesh test"),
            new("F9", "Switch to Geometry test"),
            new("F10", "Switch to Morphology test"),
            new("F11", "Switch to Triangle Algorithm test"),
            new("F12", "Switch to Branch Port test"),
            new("1 / Numpad1", "Switch to Polywrapping test"),
            new("2 / Numpad2", "Switch to Branch Assignment test"),
            new("3 / Numpad3", "Switch to Delaunay 3D test"),
            new("4 / Numpad4", "Switch to Bajaj Test"),
            new("5 / Numpad5", "Switch to Delaunay 2D test"),
            new("6 / Numpad6", "Switch to Curve Simplification test"),
            new("7 / Numpad7", "Switch to Bajaj Multi Test"),
            new("8 / Numpad8", "Switch to Constrained Delaunay 2D test"),
            new("9 / Numpad9", "Switch to Polygon Intersection test"),
            new("0 / Numpad0", "Switch to Labeled Rectangles test"),
            new("Esc", "Exit testbed"),
            new("? / Shift+F1", "Open this Help dialog (when menu is available)"),
        ];

        /// <summary>
        /// Rows for the Help dialog: Global section first, then Current test section.
        /// </summary>
        public static IReadOnlyList<HotkeyHelpSection> ForTest(TestMode mode, IGraphicsTest test)
        {
            List<HotkeyHelpSection> sections =
            [
                new("Global", GlobalBindings)
            ];

            if (test is ITestHotkeyHelp help)
            {
                sections.Add(new($"{test.Title} ({mode})", help.GetHotkeyBindings()));
            }
            else
            {
                sections.Add(new($"{test.Title} ({mode})",
                [
                    new HotkeyBinding("(none)", "No test-specific shortcuts registered.")
                ]));
            }

            return sections;
        }
    }

    /// <summary>
    /// One titled group of bindings in the Help dialog.
    /// </summary>
    public readonly record struct HotkeyHelpSection(string Title, IReadOnlyList<HotkeyBinding> Bindings);
}
