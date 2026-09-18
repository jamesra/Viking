using System;
using System.Collections.Generic;

namespace MonogameTestbed
{
    /// <summary>
    /// One per-test Settings menu item. Leaf items invoke <see cref="Apply"/>; parent items expose
    /// mutually exclusive or related choices through <see cref="Children"/>.
    /// </summary>
    sealed class TestSettingItem
    {
        public required string Label { get; init; }

        public Action Apply { get; init; }

        public Func<bool?> IsChecked { get; init; }

        public IReadOnlyList<TestSettingItem> Children { get; init; } = [];
    }

    /// <summary>Optional contract for tests that contribute items to the active test's Settings menu.</summary>
    interface ITestSettings
    {
        IReadOnlyList<TestSettingItem> GetSettings();
    }
}
