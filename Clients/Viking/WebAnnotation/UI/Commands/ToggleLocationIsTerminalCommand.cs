using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using WebAnnotationModel;
using WebAnnotation.ViewModel; 

namespace WebAnnotation.UI.Commands
{
    class ToggleLocationIsTerminalCommand : Viking.UI.Commands.Command
    {
        LocationObj target;
        public ToggleLocationIsTerminalCommand(Viking.UI.Controls.SectionViewerControl parent,
                                         LocationObj loc)
            : base(parent)
        {
            this.target = loc;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate() { this.Execute(); });
        }

        protected override void Execute()
        {
            target.Terminal = !target.Terminal;
            Store.Locations.Save();
            base.Execute();
        }
    }
}
