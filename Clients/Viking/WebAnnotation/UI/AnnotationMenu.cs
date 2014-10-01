using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.Common;
using System.Diagnostics;
using System.Windows.Forms; 

namespace WebAnnotation.UI
{
    [MenuAttribute("Annotation")]
    class AnnotationMenu
    {
        static FindStructureNumberForm _FindStructureNumberForm = null;
        static MergeStructuresForm _MergeStructuresForm = null;

        [MenuItem("Open Last Modified Location")]
        static public void GoToLastModifiedLocation(object sender, EventArgs e)
        {
            Debug.Print("Open Last Modified Location");

            WebAnnotationModel.LocationObj lastLocation = WebAnnotationModel.Store.Locations.GetLastModifiedLocation();
            if (lastLocation != null)
            {
                AnnotationOverlay.GoToLocation(lastLocation);
            }
        }

        [MenuItem("Open Structure")]
        static public void ShowStructure(object sender, EventArgs e)
        {
            Debug.Print("Show Structure");

            if (_FindStructureNumberForm == null)
            {
                _FindStructureNumberForm = new FindStructureNumberForm();
            }
            else if (_FindStructureNumberForm.IsDisposed)
            {
                _FindStructureNumberForm = new FindStructureNumberForm();
            }

            _FindStructureNumberForm.Show();
            _FindStructureNumberForm.Focus(); 
        }

        [MenuItem("Merge Structures")]
        static public void MergeStructures(object sender, EventArgs e)
        {
            Debug.Print("Merge Structures");

            if (_MergeStructuresForm == null)
            {
                _MergeStructuresForm = new MergeStructuresForm();
            }
            else if (_MergeStructuresForm.IsDisposed)
            {
                _MergeStructuresForm = new MergeStructuresForm();
            }

            _MergeStructuresForm.ShowDialog();
            _MergeStructuresForm.Focus();
        }

       
    }
}
