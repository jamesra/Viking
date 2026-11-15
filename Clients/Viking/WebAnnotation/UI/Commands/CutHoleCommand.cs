using System.Collections.ObjectModel;
using Viking.Common;

namespace WebAnnotation.UI.Commands
{
    /// <summary>Help for cutting a hole. Nothing constructs this class; it only holds the help list.</summary>
    internal class CutHoleCommand : IHelp, IObservableHelp
    {
        public static readonly HelpEntry[] DefaultCutHoleHelp =
        [
            new("Copy control points", HelpModifiers.Ctrl, Where: "over another curve"),
            HelpEntry.For(HelpInput.MiddleClick, "Reset to original size"),
            HelpEntry.For(HelpInput.RightDrag, "Rotate"),
            HelpEntry.For(HelpInput.Wheel, "Change annotation size"),
            HelpEntry.For(HelpInput.Wheel, "Scale annotation size slowly", HelpModifiers.Shift),
        ];

        public virtual HelpEntry[] Help => DefaultCutHoleHelp;

        public ObservableCollection<HelpEntry> ObservableHelp => new(Help);
    }
}
