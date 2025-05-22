using System;

using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This command takes a structureObj and LocationObj defined by other commands and commits them to the database
    /// </summary>
    internal class CreateNewStructureCommand : AnnotationCommandBase
    {
        private readonly StructureObj newStruct;
        private readonly LocationObj newLoc;

        public CreateNewStructureCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               StructureObj structure,
                                               LocationObj location)
            : base(parent)
        {
            newStruct = structure;
            newLoc = location;
        }

        public override void OnActivate()
        {
            Parent.BeginInvoke((Action)delegate () { Execute(); });
        }

        protected override void Execute()
        {
            //Create the new structure
            Store.Structures.Create(newStruct, newLoc, out LocationObj unused);
            if (unused != null)
            {
                Global.LastEditedAnnotationID = unused.ID;
            }

            base.Execute();
        }
    }
}
