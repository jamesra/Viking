using System.Collections.Generic;

namespace MonogameTestbed
{
    /// <summary>
    /// One shortcut shown in Help: the key chord and a short description of what it does.
    /// </summary>
    public readonly record struct HotkeyBinding(string Keys, string Description);

    /// <summary>
    /// Optional contract a test implements so Help can list its keyboard and gamepad shortcuts.
    /// Tests that do not implement it still show global test-switching keys.
    /// </summary>
    interface ITestHotkeyHelp
    {
        IReadOnlyList<HotkeyBinding> GetHotkeyBindings();
    }
}
