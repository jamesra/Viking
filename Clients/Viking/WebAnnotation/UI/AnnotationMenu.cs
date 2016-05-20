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
    class AnnotationMenu : Viking.Common.IMenuFactory
    {
        static FindStructureNumberForm _FindStructureNumberForm = null;
        static MergeStructuresForm _MergeStructuresForm = null;

        System.Windows.Forms.ToolStripItem Viking.Common.IMenuFactory.CreateMenuItem()
        {
            //Create a menu containing each of our bookmarks
            ToolStripMenuItem menuRoot = new ToolStripMenuItem("Annotation");

            if (Global.Export != null)
            {
                //Create the option to hide bookmarks on the display
                ToolStripMenuItem menuExport = new ToolStripMenuItem("Export");

                //Create the option to hide bookmarks on the display
                ToolStripMenuItem menuExportMotifs = new ToolStripMenuItem("Motifs");

                ToolStripMenuItem menuExportMotifTLP = new ToolStripMenuItem("To Tulip Format");
                menuExportMotifTLP.Click += OnExportMotifsTLP;

                menuExportMotifs.DropDownItems.Add(menuExportMotifTLP);
                menuExport.DropDownItems.Add(menuExportMotifs);
                menuRoot.DropDownItems.Add(menuExport);
            }

            return menuRoot as ToolStripItem; 
        }

        static public void OnExportMotifsTLP(object sender, EventArgs e)
        {
            Debug.Print("OnExportMotifsTLP");

            Global.Export.OpenMotif();
        }

        [MenuItem("Open Last Modified Location")]
        static public void GoToLastModifiedLocation(object sender, EventArgs e)
        {
            AnnotationOverlay.GotoLastModifiedLocation();
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

        [MenuItem("Export")]
        static public void Export(object sender, EventArgs e)
        {
            Debug.Print("Export");

            
        }

       
    }
}
