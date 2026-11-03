using System;
using System.Windows.Input;

namespace WebAnnotation.WPF.Forms
{
    /// <summary>
    /// Maps the button a pen reports to the stored barrel button name used by the preferences
    /// (see <see cref="AnnotationPreferencesDialogViewModel.BarrelButton"/>).
    /// </summary>
    public static class StylusButtonCapture
    {
        /// <summary>
        /// The stored name for the pen button with id <paramref name="stylusButtonId"/>: "Barrel" for the barrel button,
        /// "Eraser" for the secondary tip (eraser) button, or null for any other button.
        /// </summary>
        public static string NameFor(Guid stylusButtonId)
        {
            if (stylusButtonId == StylusPointProperties.BarrelButton.Id)
                return "Barrel";

            if (stylusButtonId == StylusPointProperties.SecondaryTipButton.Id)
                return "Eraser";

            return null;
        }
    }
}
