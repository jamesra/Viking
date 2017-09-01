using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.Common;
using System.Xml.Linq;
using System.IO; 
using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using System.Diagnostics;
using Geometry;

namespace LocalBookmarks
{
    class Global : IInitExtensions
    {
        static string BookmarkPath = Viking.UI.State.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "Bookmarks";

        /// <summary>
        /// Bookmark filename only
        /// </summary>
        static string BookmarkSaveTestFileName = "BookmarkSaveTest.xml";

        /// <summary>
        /// Bookmark filename only
        /// </summary>
        static string BookmarkFileName = "Bookmarks.xml";

        /// <summary>
        /// Undo filename only
        /// </summary>
        static string BookmarkUndoFileName = "BookmarksUndo01.xml";

        static string BookmarkSaveTestFilePath = BookmarkPath + System.IO.Path.DirectorySeparatorChar + BookmarkSaveTestFileName; 
        /// <summary>
        /// The full name of the bookmark file including filename and path
        /// </summary>
        static string BookmarkFilePath = BookmarkPath + System.IO.Path.DirectorySeparatorChar + BookmarkFileName;

        /// <summary>
        /// The full name of the undo file including filename and path
        /// </summary>
        static string BookmarkUndoFilePath = BookmarkPath + System.IO.Path.DirectorySeparatorChar + BookmarkUndoFileName;

        static internal readonly string XSDUri = "http://connectomes.utah.edu/XSD/BookmarkSchema.xsd"; 

        /// <summary>
        /// The number of undo files to maintain
        /// </summary>
        //static readonly int UndoDepth = 16;

        internal static XRoot BookmarkXMLDoc;
        
        private static Folder FolderRoot
        {
            get { return BookmarkXMLDoc.Folder;}
        }

        static public double DefaultBookmarkRadius = 128;
        static public double BookmarkArea = DefaultBookmarkRadius * DefaultBookmarkRadius * Math.PI; 

        private static FolderUIObj _SelectedFolder;
        public static FolderUIObj SelectedFolder
        {
            get { return _SelectedFolder; }
            set { _SelectedFolder = value; }
        }

        internal static FolderUIObj FolderUIObjRoot = null;

        internal static bool BookmarksVisible = true; 

        public static event EventHandler AfterUndo; 


        internal static void Save()
        {
            try
            {
                //If we are low on memory we could fail at any point.  Ensure that the original file is preserved until the new file is written.
                string newXMLFile = BookmarkXMLDoc.XDocument.ToString(); 
                
                //Create a backup in case this was a horrible mistake
                if (System.IO.File.Exists(BookmarkUndoFilePath))
                {
                    System.IO.File.Delete(BookmarkUndoFilePath);
                }

                try
                {
                    if(System.IO.File.Exists(BookmarkFilePath))
                        System.IO.File.Move(BookmarkFilePath, BookmarkUndoFilePath);
                }
                catch (System.IO.FileNotFoundException )
                {
                    System.Windows.Forms.MessageBox.Show("Tell James Viking told you it could not create undo file for bookmarks.");
                }

                //Save the Bookmark file
                using (StreamWriter saveFile = new StreamWriter(BookmarkFilePath))
                {
                    saveFile.Write(newXMLFile);
                }
            }
            catch(Exception e)
            {
                Trace.WriteLine("An exception occurred saving the bookmark file");
                Trace.WriteLine(e.Message);
                Trace.WriteLine(e.ToString());
                if (e.InnerException != null)
                {
                    Trace.WriteLine("    Inner Exception");
                    Trace.WriteLine("   " + e.InnerException.ToString());
                }
                
                System.Windows.Forms.MessageBox.Show("An exception occurred saving the bookmark file: " + e.ToString());

                throw e; 
            }

            if (System.IO.File.Exists(BookmarkFilePath) == false)
            {
                System.Windows.Forms.MessageBox.Show("For some reason Viking can't find: " + BookmarkFilePath + "\nViking just tried to save this file.  You should  use the \"Export->XML\" menu option from the bookmarks tab to create a backup just in case. This is an unexplained bug we're working on.  The last change was not saved.");
                System.IO.File.Move(BookmarkUndoFilePath, BookmarkFilePath); 
            }

            Viking.UI.State.ViewerForm.Invalidate();
            Viking.UI.State.ViewerControl.Invalidate();
        }

        internal static void Save(string SavePath)
        {
            BookmarkXMLDoc.Save(SavePath); 
        }

        internal static void Undo()
        {
            
            if (System.IO.File.Exists(BookmarkUndoFilePath))
            {
                //Swap the current and undo versions of the bookmark file
                string TempFileName = Viking.UI.State.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "Temp.xml";
                if (System.IO.File.Exists(TempFileName))
                    System.IO.File.Delete(TempFileName);

                System.IO.File.Move(BookmarkFilePath, TempFileName);
                System.IO.File.Move(BookmarkUndoFilePath, BookmarkFilePath);
                System.IO.File.Move(Viking.UI.State.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "Temp.xml", BookmarkUndoFilePath);

                //Reload the bookmarks
                Global global = new Global();
                global.Initialize();

                if (AfterUndo != null)
                    AfterUndo(Global.FolderUIObjRoot, new EventArgs());

                Viking.UI.State.ViewerControl.Invalidate();
            }


        }

        /// <summary>
        /// The number of undo steps we have available
        /// </summary>
        /// <returns></returns>
        internal static string[] UndoFileNames()
        {
            string SearchString = string.Format(BookmarkUndoFileName, '*');
            string[] UndoFiles = System.IO.Directory.GetFiles(BookmarkPath, SearchString);
            return UndoFiles; 
        }

        #region IInitExtensions Members

        public bool Initialize()
        {
            //Check if there is a local favorites XML file, if it does not exist, create it, we always return true

            try

            {
                if (false == System.IO.Directory.Exists(BookmarkPath))
                {
                    System.IO.Directory.CreateDirectory(BookmarkPath);
                }

                if (false == System.IO.File.Exists(BookmarkFilePath))
                {
                    bool Restored = LoadBookmarksFromBackup();
                    if (!Restored)
                    {
                        BookmarkXMLDoc = CreateNewBookmarkFile();
                    }
                }
                else
                {
                    BookmarkXMLDoc = XRoot.Load(BookmarkFilePath);
                }
            }
            catch (System.IO.FileNotFoundException )
            {
                BookmarkXMLDoc = CreateNewBookmarkFile();
            }
            catch (Xml.Schema.Linq.LinqToXsdException )
            {
                //We found it, but could not parse it.  Check if it is an old file that needs an upgrade
                try
                {
                    connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot oldRoot = connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot.Load(BookmarkFilePath);
                    BookmarkXMLDoc = MigrateV1ToV2.Migrate(BookmarkFilePath);
                    if (BookmarkXMLDoc == null)
                    {
                        BookmarkXMLDoc = CreateNewBookmarkFile();
                    }
                    else
                    {
                        Save();
                    }

                    FolderUIObjRoot = new FolderUIObj(null, FolderRoot);
                    SelectedFolder = FolderUIObjRoot;
                }
                catch(Xml.Schema.Linq.LinqToXsdException)
                {
                    //OK, could not load with the old schema.
                    HandleIncorrectXSDMessage();
                    LoadBookmarksFromBackup();
                } 
            }
            catch (System.Xml.XmlException )
            {
                //We found it, but could not parse it
                HandleIncorrectXSDMessage();
                LoadBookmarksFromBackup();
            }
            catch (Exception )
            {
                //We found it, but could not parse it
              //  HandleIncorrectXSDMessage();
              //  LoadBookmarksFromBackup();
            }

            FolderUIObjRoot = new FolderUIObj(null, FolderRoot);
            SelectedFolder = FolderUIObjRoot; 
            
            return true; 
        }

        

        public static XRoot CreateNewBookmarkFile()
        {
            Folder newFolderRoot = new Folder();
            newFolderRoot.Name = "root";
            XRoot root = new XRoot(newFolderRoot);
            root.Save(BookmarkFilePath);
            return root;
        }

        private static void HandleIncorrectXSDMessage()
        {
            //We found it, but could not parse it
                string BookmarkRefuge = BookmarkPath + Path.DirectorySeparatorChar + "InvalidSchemaBookmark.xml";
                System.IO.File.Move(BookmarkFilePath, BookmarkPath + Path.DirectorySeparatorChar + "InvalidSchemaBookmark.xml");
                System.Windows.Forms.MessageBox.Show("I could not read your bookmark.xml file, so I moved it to: \n" + BookmarkRefuge + "\n"+
                                                "You can probably recover them by closing Viking and setting/replacing the xmnls attribute on the \"root\" element to:\n"+
                                                "xmlns=\"http://tempuri.org/BookmarkSchema.xsd\" and replacing the Bookmarks.xml with it.");
        }

        public static bool Load(string BookmarkFileName)
        {
            try
            {
                if (System.IO.File.Exists(BookmarkFileName))
                {
                    BookmarkXMLDoc = XRoot.Load(BookmarkFileName);
                    RecursivelyUpdateVolumePositions(FolderRoot);
                    FolderUIObjRoot = new FolderUIObj(null, FolderRoot);
                    SelectedFolder = FolderUIObjRoot;
                    return true;
                }
                else if(System.IO.File.Exists(BookmarkUndoFilePath)) //Check for an undo file
                {
                    BookmarkXMLDoc = XRoot.Load(BookmarkUndoFilePath);
                    RecursivelyUpdateVolumePositions(FolderRoot);
                    FolderUIObjRoot = new FolderUIObj(null, FolderRoot);
                    SelectedFolder = FolderUIObjRoot;
                    return true;
                }
            }
            catch (Xml.Schema.Linq.LinqToXsdException)
            {
                //We found it, but could not parse it.  Check if it needs to be migrated.
                try
                {
                    connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot oldRoot = connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot.Load(BookmarkFileName);
                    BookmarkXMLDoc = MigrateV1ToV2.Migrate(BookmarkFileName);
                    FolderUIObjRoot = new FolderUIObj(null, FolderRoot);
                    SelectedFolder = FolderUIObjRoot;
                }
                catch (Xml.Schema.Linq.LinqToXsdException)
                {
                    //OK, could not load with the old schema.
                    HandleIncorrectXSDMessage(); 
                }
            }
            catch
            {

            }


            return false; 
        }

        private static bool LoadBookmarksFromBackup()
        {
            if (System.IO.File.Exists(BookmarkUndoFilePath))
            {
                BookmarkXMLDoc = XRoot.Load(Global.BookmarkUndoFilePath);
                return true; 
            }
            else
            {
                BookmarkXMLDoc = CreateNewBookmarkFile();
                return false; 
            }
        }

        /// <summary>
        /// Recursively update all bookmark positions with the new transform
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="transform"></param>
        public static void RecursivelyUpdateVolumePositions(Folder folder)
        {
            foreach(var bookmark in folder.Bookmarks)
            {
                GridVector2 sectionPosition;
                Viking.VolumeModel.IVolumeToSectionTransform transform = Viking.UI.State.volume.GetSectionToVolumeTransform((int)bookmark.Z);
                if (transform.TrySectionToVolume(bookmark.MosaicPosition.ToGridVector2(), out sectionPosition))
                {
                    bookmark.VolumePosition = new Point2D(sectionPosition);
                }
            }

            foreach(var subfolder in folder.Folders)
            {
                RecursivelyUpdateVolumePositions(subfolder);
            }

            return;
        }

        /// <summary>
        /// When this occurs we should update the positions we draw the locations at. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static void OnVolumeTransformChanged(object sender, TransformChangedEventArgs e)
        {
            Global.RecursivelyUpdateVolumePositions(FolderRoot);
        }

        

        #endregion
    }
}
