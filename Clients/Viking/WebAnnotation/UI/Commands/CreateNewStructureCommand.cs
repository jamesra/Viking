using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;

using WebAnnotationModel;
using WebAnnotation.ViewModel; 

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This command takes a structureObj and LocationObj defined by other commands and commits them to the database
    /// </summary>
    class CreateNewStructureCommand : AnnotationCommandBase
    {
        Structure newStruct;
        Location_CanvasViewModel newLoc;

        public CreateNewStructureCommand(Viking.UI.Controls.SectionViewerControl parent, 
                                               Structure structure, 
                                               Location_CanvasViewModel location)
            : base(parent)
        {
            this.newStruct = structure;
            this.newLoc = location;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate() { this.Execute(); });
        }

        protected override void Execute()
        {
           //Create the new structure
            LocationObj unused;
            Store.Structures.Create(newStruct.modelObj, newLoc.modelObj, out unused);

           base.Execute();
        }
    }
}
