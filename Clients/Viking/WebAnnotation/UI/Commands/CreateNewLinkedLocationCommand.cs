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
                Store.Locations.Add(NewLoc.modelObj);

                //Save the new location
                bool Success = Store.Locations.Save();
                if (Success)
                {
                    //Create a link between the two objects
                    Store.LocationLinks.CreateLink(NewLoc.ID, ExistingLoc.ID);
                    LastEditedLocation = NewLoc;
                }
                else
                {
                    //Remove the new location
                    Store.Locations.Remove(NewLoc.modelObj);
                    LastEditedLocation = null; //Clear the last edited location
                }
            }
            catch (ArgumentOutOfRangeException )
            {
                MessageBox.Show("The chosen point is outside mappable volume space, location not created", "Recoverable Error");
            }

            base.Execute();
        }
    }
}
