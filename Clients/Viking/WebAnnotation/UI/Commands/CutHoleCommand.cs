using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    class CutHoleCommand : Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        MeshView<VertexPositionColor> meshView = null;

        public static string[] DefaultCutHoleHelpStrings = new string[]
        {
            "CTRL+Click another curve: Copy control points",
            "Middle Button click: Reset to original size",
            "Hold Right click and drag: Rotate",
            "Mouse Wheel: Change annotation size",
            "SHIFT + Scroll wheel: Scale annotation size slowly"
        };

        public virtual string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>(CutHoleCommand.DefaultCutHoleHelpStrings);
                s.AddRange(TranslateScaleCommandBase.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                s.Sort();
                return s.ToArray();
            }
        }

        public ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }
    }
}
