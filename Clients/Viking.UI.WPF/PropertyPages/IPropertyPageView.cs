using System.Windows;

namespace Viking.UI.WPF.PropertyPages
{
    /// <summary>
    /// Contract implemented by WPF-based property pages so the host window can
    /// initialize, validate, and persist changes consistently.
    /// </summary>
    public interface IPropertyPageView
    {
        /// <summary>
        /// Header text displayed on the containing tab.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// The WPF element that should be placed inside the tab.
        /// Usually returns the control that implements this interface.
        /// </summary>
        FrameworkElement View { get; }

        /// <summary>
        /// Called when the property sheet prepares the page for a new target object.
        /// </summary>
        /// <param name="context">The object whose properties should be shown.</param>
        void Initialize(object context);

        /// <summary>
        /// Give the page a chance to veto saving (e.g., invalid input).
        /// Returning false will keep the sheet open and focus the page.
        /// </summary>
        bool ValidateChanges();

        /// <summary>
        /// Commit pending changes to the backing object.
        /// </summary>
        void SaveChanges();

        /// <summary>
        /// Revert any temporary state accumulated by the page.
        /// </summary>
        void CancelChanges();
    }
}

