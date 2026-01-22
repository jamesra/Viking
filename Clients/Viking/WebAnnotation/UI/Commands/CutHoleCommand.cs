using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    internal class CutHoleCommand : Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        private readonly MeshView<VertexPositionColor>? meshView = null;

        public static string[] DefaultCutHoleHelpStrings =
        [
            "CTRL+Click another curve: Copy control points",
            "Middle Button click: Reset to original size",
            "Hold Right click and drag: Rotate",
            "Mouse Wheel: Change annotation size",
            "SHIFT + Scroll wheel: Scale annotation size slowly"
        ];

        public virtual string[] HelpStrings
        {
            get
            {
                List<string> s = [.. CutHoleCommand.DefaultCutHoleHelpStrings];
                s.AddRange(TranslateScaleCommandBase.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                s.Sort();
                return [.. s];
            }
        }

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);
    }
}
