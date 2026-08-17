using System;
using System.Threading;
using System.Windows.Forms;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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
        int _started;

        public override void OnActivate() => Parent.BeginInvoke((Action)Execute);

        protected override void Execute()
        {
            _ = ExecuteAsync();
        }

        async System.Threading.Tasks.Task ExecuteAsync()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                return;

            try
            {
                if (!LocationLinkView.IsValidLocationLinkTarget(NewLoc, ExistingLoc))
                {
                    MessageBox.Show("The new linked location must be on a different section.  Location links cannot be linked on the same section.\n(Perhaps a polygon would be appropriate if it is a long thin shape?)", "Recoverable Error");
                    CancelCommand();
                    return;
                }

                LocationObj NewLocation = await Store.Locations.Create(NewLoc, [ExistingLoc.ID]);
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
