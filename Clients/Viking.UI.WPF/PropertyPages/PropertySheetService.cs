using System;
using System.Diagnostics;
using System.Windows;

namespace Viking.UI.WPF.PropertyPages
{
    public static class PropertySheetService
    {
        /// <summary>
        /// Displays the property sheet for the provided target using the attribute-based page registry.
        /// </summary>
        /// <param name="target">Object whose properties are being edited.</param>
        /// <param name="owner">Optional owner window.</param>
        public static bool? ShowDialog(object target, Window owner = null)
        {
            if (target is null)
            {
                return false;
            }

            // Diagnostic logging
            try
            {
                var resAsm = Application.ResourceAssembly;
                var currentApp = Application.Current;
                Trace.WriteLine($"PropertySheetService: ResourceAssembly = {resAsm?.FullName ?? "null"}", "Viking.UI.WPF");
                Trace.WriteLine($"PropertySheetService: Current Application = {currentApp?.GetType().FullName ?? "null"}", "Viking.UI.WPF");
                Trace.WriteLine($"PropertySheetService: This Assembly = {typeof(PropertySheetWindow).Assembly.FullName}", "Viking.UI.WPF");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"PropertySheetService diagnostic error: {ex}", "Viking.UI.WPF");
            }

            PropertySheetWindow window = new PropertySheetWindow
            {
                Owner = owner
            };

            window.Initialize(target);
            return window.ShowDialog();
        }
    }
}

