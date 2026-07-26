using System;
using System.Windows.Forms;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{

    /// <summary>
    /// This command takes two LocationObj, an existing and a new one
    /// Defined by other commands and commits them to the database
    /// </summary>
    internal class CreateNewLinkedLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                           LocationObj existingLoc,
                                           LocationObj newLoc) : Viking.UI.Commands.Command(parent)
    {
        private readonly LocationObj NewLoc = newLoc;
        private readonly LocationObj ExistingLoc = existingLoc;

        public override void OnActivate() => Parent.BeginInvoke((Action)delegate () { Execute(); });

        protected override void Execute()
        {
            try
            {
                if (!LocationLinkView.IsValidLocationLinkTarget(NewLoc, ExistingLoc))
                {
                    MessageBox.Show("The new linked location must be on a different section.  Location links cannot be linked on the same section.\n(Perhaps a polygon would be appropriate if it is a long thin shape?)", "Recoverable Error");
                    CancelCommand();
                    return;
                }

                LocationObj NewLocation = Store.Locations.Create(NewLoc, [ExistingLoc.ID]);
                Global.LastEditedAnnotationID = NewLocation.ID;
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("The chosen point is outside mappable volume space, location not created", "Recoverable Error");
                return;
            }

            base.Execute();
        }
    }
}
