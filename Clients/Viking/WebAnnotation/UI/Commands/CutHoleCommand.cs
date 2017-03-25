using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using Geometry;
using WebAnnotation.View;
using Viking.VolumeModel;
using SqlGeometryUtils;
using VikingXNAGraphics;
using System.Windows.Forms;
using System.Diagnostics;
using WebAnnotation;
using System.Collections.ObjectModel;
using VikingXNAWinForms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
