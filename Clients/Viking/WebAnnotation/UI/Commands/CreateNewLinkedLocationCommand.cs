using System;
using System.Windows.Forms;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Commands
{

    /// <summary>
    /// This command takes two LocationObj, an existing and a new one
    /// Defined by other commands and commits them to the database
    /// </summary>
    class CreateNewLinkedLocationCommand : Viking.UI.Commands.Command
    {
        LocationObj NewLoc;
        LocationObj ExistingLoc;

        public CreateNewLinkedLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               LocationObj existingLoc,
                                               LocationObj newLoc)
            : base(parent)
        {
            this.NewLoc = newLoc;
            this.ExistingLoc = existingLoc;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        }

        protected override void Execute()
        {
            try
            {
                LocationObj NewLocation = Store.Locations.Create(NewLoc, new long[] { ExistingLoc.ID });
                Global.LastEditedAnnotationID = NewLocation.ID;
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("The chosen point is outside mappable volume space, location not created", "Recoverable Error");
            }

            base.Execute();
        }
    }
}
