using System;

using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This command takes a structureObj and LocationObj defined by other commands and commits them to the database
    /// </summary>
    class CreateNewStructureCommand : AnnotationCommandBase
    {
        StructureObj newStruct;
        LocationObj newLoc;

        public CreateNewStructureCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               StructureObj structure,
                                               LocationObj location)
            : base(parent)
        {
            this.newStruct = structure;
            this.newLoc = location;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        }

        protected override void Execute()
        {
            //Create the new structure
            LocationObj unused;
            Store.Structures.Create(newStruct, newLoc, out unused);
            if (unused != null)
                Global.LastEditedAnnotationID = unused.ID;
            base.Execute();
        }
    }
}
