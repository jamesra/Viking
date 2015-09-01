using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using WebAnnotationModel;
using WebAnnotation.ViewModel; 

namespace WebAnnotation.UI.Commands
{
    class ToggleTagCommand : Viking.UI.Commands.Command
    {
        Structure target;
        string tag;
        public ToggleTagCommand(Viking.UI.Controls.SectionViewerControl parent, 
                                         Structure structure,
                                         string Tag)
            : base(parent)
        {
            this.target = structure;
            this.tag = Tag;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate() { this.Execute(); });
        }

        protected override void Execute()
        {
            ObjAttribute attrib = new ObjAttribute(this.tag, null);
            List<ObjAttribute> listAttributes = target.Attributes.ToList();
            if(listAttributes.Contains(attrib))
            {
                listAttributes.Remove(attrib);
            }
            else
            {
                listAttributes.Add(attrib);
            }

            target.Attributes = listAttributes;

            Store.Structures.Save();

            base.Execute();
        }
    }
}
