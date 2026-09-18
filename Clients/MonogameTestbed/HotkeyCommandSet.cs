using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace MonogameTestbed
{
    /// <summary>
    /// One hotkey: Help display text plus the edge-detect and action used at runtime.
    /// Help is a projection of this table so documentation cannot drift from <see cref="HotkeyCommandSet.Process"/>.
    /// </summary>
    sealed class HotkeyCommand
    {
        public required string Keys { get; init; }

        public required string Description { get; init; }

        /// <summary>
        /// Returns true on the frame the binding should fire. Null means help-only (e.g. mouse pick
        /// handled elsewhere); <see cref="HotkeyCommandSet.Process"/> skips it.
        /// </summary>
        public Func<TestInputContext, bool> Fired { get; init; }

        public Action Invoke { get; init; }

        /// <summary>When false, omit from Help and skip Process. Null means always active.</summary>
        public Func<bool> IsActive { get; init; }

        /// <summary>
        /// When false the command still runs in <see cref="HotkeyCommandSet.Process"/> but is omitted from Help
        /// (use for extra gamepad buttons covered by a grouped help row).
        /// </summary>
        public bool IncludeInHelp { get; init; } = true;
    }

    /// <summary>
    /// Per-test table of hotkeys. Own one of these, register commands once, call <see cref="Process"/>
    /// from Update, and expose <see cref="ToHelpBindings"/> via <see cref="ITestHotkeyHelp"/>.
    /// </summary>
    sealed class HotkeyCommandSet
    {
        readonly List<HotkeyCommand> _commands = [];

        public void Add(HotkeyCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (string.IsNullOrWhiteSpace(command.Keys))
                throw new ArgumentException("Keys display text is required.", nameof(command));
            if (string.IsNullOrWhiteSpace(command.Description))
                throw new ArgumentException("Description is required.", nameof(command));

            _commands.Add(command);
        }

        public void Add(
            string keys,
            string description,
            Func<TestInputContext, bool> fired,
            Action invoke,
            Func<bool> isActive = null,
            bool includeInHelp = true)
        {
            Add(new HotkeyCommand
            {
                Keys = keys,
                Description = description,
                Fired = fired,
                Invoke = invoke,
                IsActive = isActive,
                IncludeInHelp = includeInHelp
            });
        }

        /// <summary>Document a binding handled outside this set (still listed in Help).</summary>
        public void AddHelpOnly(string keys, string description, Func<bool> isActive = null)
        {
            Add(new HotkeyCommand
            {
                Keys = keys,
                Description = description,
                Fired = null,
                Invoke = null,
                IsActive = isActive,
                IncludeInHelp = true
            });
        }

        public static Func<TestInputContext, bool> Key(Keys key) =>
            input => input.Keyboard.Pressed(key);

        public static Func<TestInputContext, bool> KeyOr(
            Keys key,
            Func<TestInputContext, bool> other) =>
            input => input.Keyboard.Pressed(key) || other(input);

        public IReadOnlyList<HotkeyBinding> ToHelpBindings() =>
            [.. _commands
                .Where(c => c.IncludeInHelp && (c.IsActive?.Invoke() ?? true))
                .Select(c => new HotkeyBinding(c.Keys, c.Description))];

        public void Process(TestInputContext input)
        {
            ArgumentNullException.ThrowIfNull(input);

            foreach (HotkeyCommand command in _commands)
            {
                if (command.Fired is null || command.Invoke is null)
                    continue;
                if (!(command.IsActive?.Invoke() ?? true))
                    continue;
                if (command.Fired(input))
                    command.Invoke();
            }
        }
    }
}
