using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Diagnostics;

using WebAnnotation.ViewModel; 
using WebAnnotationModel; 

namespace WebAnnotation.UI.Commands
{

    /// <summary>
    /// This command takes two LocationObj, an existing and a new one
    /// Defined by other commands and commits them to the database
    /// </summary>
    class CreateNewLinkedLocationCommand : Viking.UI.Commands.Command
    {
        Location_CanvasViewModel NewLoc;
        Location_CanvasViewModel ExistingLoc;

        public static Location_CanvasViewModel LastEditedLocation = null; 

        public CreateNewLinkedLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               Location_CanvasViewModel existingLoc,
                                               Location_CanvasViewModel newLoc)
            : base(parent)
        {
            this.NewLoc = newLoc;
            this.ExistingLoc = existingLoc;
        }

        public override void OnActivate()
        {
            this.Parent.BeginInvoke((Action)delegate() { this.Execute(); }); 
        }

        protected override void Execute()
        {
            try
            {
                LocationObj NewLocation = Store.Locations.Create(NewLoc.modelObj, new long[] { ExistingLoc.ID });
                LastEditedLocation = new Location_CanvasViewModel(NewLocation); 
            }
            catch (ArgumentOutOfRangeException )
            {
                MessageBox.Show("The chosen point is outside mappable volume space, location not created", "Recoverable Error");
            }

            base.Execute();
        }
    }
}
