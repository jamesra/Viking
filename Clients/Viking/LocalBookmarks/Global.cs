using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using Geometry;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;

namespace LocalBookmarks
{
    public class Global : IInitExtensions
    {
        static readonly string BookmarkPath = Viking.UI.State.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "Bookmarks";

        /// <summary>
        /// Bookmark filename only
        /// </summary>
        static readonly string BookmarkSaveTestFileName = "BookmarkSaveTest.xml";

        /// <summary>
        /// Bookmark filename only
        /// </summary>
        static readonly string BookmarkFileName = "Bookmarks.xml";

        /// <summary>
        /// Undo filename only
        /// </summary>
        static readonly string BookmarkUndoFileName = "BookmarksUndo01.xml";

        static readonly string BookmarkSaveTestFilePath = BookmarkPath + System.IO.Path.DirectorySeparatorChar + BookmarkSaveTestFileName;
        /// <summary>
        /// The full name of the bookmark file including filename and path
        /// </summary>
        static readonly string BookmarkFilePath = BookmarkPath + System.IO.Path.DirectorySeparatorChar + BookmarkFileName;

        /// <summary>
        /// The full name of the undo file including filename and path
        /// </summary>
        static readonly string BookmarkUndoFilePath = BookmarkPath + System.IO.Path.DirectorySeparatorChar + BookmarkUndoFileName;

        internal static readonly string XSDUri = "http://connectomes.utah.edu/XSD/BookmarkSchema.xsd";

        private static BookmarkDocumentStore? _store;

        /// <summary>
        /// Session document list. Local is always index 0. Throws if <see cref="Initialize"/> has not run.
        /// </summary>
        internal static BookmarkDocumentStore Documents =>
            _store ?? throw new InvalidOperationException("Bookmark documents have not been initialized.");

        internal static bool HasDocuments => _store is not null;

        internal static event EventHandler? DocumentsChanged;

        /// <summary>
        /// Local document XML. Setter replaces Local only; extras stay loaded.
        /// </summary>
        internal static XRoot BookmarkXMLDoc
        {
            get => Documents.Local.Xml;
            set => ReplaceLocalXml(value);
        }

        public static event System.ComponentModel.PropertyChangedEventHandler RootBookmarkChanged;

        internal static Folder FolderRoot => BookmarkXMLDoc.Folder;

        public static double DefaultBookmarkRadius = 128;
        public static Microsoft.Xna.Framework.Color DefaultColor = Microsoft.Xna.Framework.Color.Gold;
        public static double BookmarkArea = DefaultBookmarkRadius * DefaultBookmarkRadius * Math.PI;

        private static FolderUIObj _SelectedFolder;
        internal static FolderUIObj SelectedFolder
        {
            get => _SelectedFolder;
            set => _SelectedFolder = value;
        }

        /// <summary>
        /// Local file-root UI. Kept as the default parent for new bookmarks when nothing else is selected.
        /// </summary>
        internal static FolderUIObj FolderUIObjRoot
        {
            get => Documents.Local.Root ?? throw new InvalidOperationException("Local bookmark UI has not been attached.");
            set
            {
                Documents.Local.Root = value;
                RaiseRootChanged();
                RaiseDocumentsChanged();
            }
        }

        internal static bool BookmarksVisible = true;

        public static event EventHandler AfterUndo;

        /// <summary>
        /// Folder that should own a newly placed bookmark: the selected folder, the parent of a selected
        /// bookmark, or Local. Viewer "Add Bookmark" uses this so extra files can receive new marks.
        /// </summary>
        internal static FolderUIObj TargetFolderForNewBookmark()
        {
            if (Viking.UI.State.SelectedObject is FolderUIObj folder)
                return folder;

            if (Viking.UI.State.SelectedObject is BookmarkUIObj bookmark)
                return bookmark.Parent;

            return FolderUIObjRoot;
        }

        internal static BookmarkDocument FindOwner(FolderUIObj? folder) => Documents.FindOwner(folder);

        /// <summary>
        /// Writes Local (with undo) or an extra file (in place). Callers that used <see cref="Save()"/>
        /// for every edit now go through <see cref="SaveOwningDocument"/>.
        /// </summary>
        internal static void SaveDocument(BookmarkDocument document)
        {
            if (document.IsLocal)
                Save();
            else
                Documents.WriteXml(document);
        }

        internal static void SaveOwningDocument(FolderUIObj? folder)
        {
            if (_store is null)
                return;

            SaveDocument(FindOwner(folder));
        }

        /// <summary>
        /// Saves the volume-cache Local file and rotates the single undo copy. Extra files are not written.
        /// </summary>
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
                    if (System.IO.File.Exists(BookmarkFilePath))
                        System.IO.File.Move(BookmarkFilePath, BookmarkUndoFilePath);
                }
                catch (System.IO.FileNotFoundException)
                {
                    System.Windows.Forms.MessageBox.Show("Tell James Viking told you it could not create undo file for bookmarks.");
                }

                //Save the Bookmark file
                using StreamWriter saveFile = new(BookmarkFilePath);
                saveFile.Write(newXMLFile);
            }
            catch (Exception e)
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

                throw;
            }

            if (System.IO.File.Exists(BookmarkFilePath) == false)
            {
                System.Windows.Forms.MessageBox.Show("For some reason Viking can't find: " + BookmarkFilePath + "\nViking just tried to save this file.  You should  use the \"Export->XML\" menu option from the bookmarks tab to create a backup just in case. This is an unexplained bug we're working on.  The last change was not saved.");
                System.IO.File.Move(BookmarkUndoFilePath, BookmarkFilePath);
            }

            Viking.UI.State.ViewerForm?.Invalidate();

            Viking.UI.State.ViewerControl?.Invalidate();

        }

        internal static void Save(string SavePath) => BookmarkXMLDoc.Save(SavePath);

        /// <summary>
        /// Loads an extra bookmark XML as a top-level filename node. Does not replace Local.
        /// Already-loaded paths are ignored. Missing or unreadable files return false.
        /// </summary>
        public static bool TryLoadExtraDocument(string bookmarkFileName)
        {
            return TryLoadExtraDocument(bookmarkFileName, persistSidecar: true, showErrors: true);
        }

        internal static bool TryLoadExtraDocument(string bookmarkFileName, bool persistSidecar, bool showErrors)
        {
            if (_store is null || string.IsNullOrWhiteSpace(bookmarkFileName))
                return false;

            BookmarkDocument? already = Documents.FindByPath(bookmarkFileName);
            if (already != null)
                return true;

            XRoot? xml = TryLoadXml(bookmarkFileName, showErrors);
            if (xml is null)
                return false;

            if (!Documents.TryAddExtra(bookmarkFileName, xml, out BookmarkDocument document))
                return document.IsLocal || document.Root != null;

            RecursivelyUpdateVolumePositions(document.Folder);
            document.Root = new FolderUIObj(null, document.Folder, document);
            if (persistSidecar)
                Documents.PersistSidecar();

            RaiseDocumentsChanged();
            Viking.UI.State.ViewerControl?.Invalidate();
            return true;
        }

        /// <summary>
        /// Unloads an extra file from the session. Local cannot be removed. The file on disk is kept.
        /// </summary>
        internal static bool TryUnloadDocument(BookmarkDocument document)
        {
            if (_store is null || document is null || document.IsLocal)
                return false;

            if (!Documents.TryUnload(document))
                return false;

            if (ReferenceEquals(SelectedFolder, document.Root) || IsUnder(SelectedFolder, document.Root))
                SelectedFolder = FolderUIObjRoot;

            Documents.PersistSidecar();
            RaiseDocumentsChanged();
            Viking.UI.State.ViewerControl?.Invalidate();
            return true;
        }

        internal static void PromptAndLoadExtraDocuments()
        {
            using OpenFileDialog fileDialog = new()
            {
                DefaultExt = ".xml",
                Title = "Open Bookmark XML File",
                CheckFileExists = true,
                AddExtension = true,
                AutoUpgradeEnabled = true,
                Multiselect = true,
                Filter = "Bookmark XML (*.xml)|*.xml|All files (*.*)|*.*"
            };

            if (DialogResult.OK != fileDialog.ShowDialog())
                return;

            foreach (string fileName in fileDialog.FileNames)
                TryLoadExtraDocument(fileName);
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
                Global global = new();
                global.Initialize(null);

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

        public bool Initialize(IServiceProvider? provider = null)
        {
            //Check if there is a local favorites XML file, if it does not exist, create it, we always return true

            try
            {
                if (false == System.IO.Directory.Exists(BookmarkPath))
                {
                    System.IO.Directory.CreateDirectory(BookmarkPath);
                }

                XRoot localXml;
                if (false == System.IO.File.Exists(BookmarkFilePath))
                {
                    localXml = TryReadUndoXml() ?? CreateNewBookmarkFile();
                }
                else
                {
                    localXml = XRoot.Load(BookmarkFilePath);
                }

                InstallLocalStore(localXml);
                LoadSidecarExtras();
            }
            catch (System.IO.FileNotFoundException)
            {
                InstallLocalStore(CreateNewBookmarkFile());
                LoadSidecarExtras();
            }
            catch (Xml.Schema.Linq.LinqToXsdException)
            {
                //We found it, but could not parse it.  Check if it is an old file that needs an upgrade
                try
                {
                    _ = connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot.Load(BookmarkFilePath);
                    XRoot? migrated = MigrateV1ToV2.Migrate(BookmarkFilePath);
                    InstallLocalStore(migrated ?? CreateNewBookmarkFile());
                    if (migrated != null)
                        Save();
                }
                catch (Xml.Schema.Linq.LinqToXsdException)
                {
                    HandleIncorrectXSDMessage();
                    InstallLocalStore(TryReadUndoXml() ?? CreateNewBookmarkFile());
                }

                LoadSidecarExtras();
            }
            catch (System.Xml.XmlException)
            {
                HandleIncorrectXSDMessage();
                InstallLocalStore(TryReadUndoXml() ?? CreateNewBookmarkFile());
                LoadSidecarExtras();
            }
            catch (Exception)
            {
                //We found it, but could not parse it
                //  HandleIncorrectXSDMessage();
                //  LoadBookmarksFromBackup();
            }

            return true;
        }

        public static XRoot CreateNewBookmarkFile()
        {
            Folder newFolderRoot = new()
            {
                Name = "root"
            };
            XRoot root = new(newFolderRoot);
            root.Save(BookmarkFilePath);
            return root;
        }

        private static void HandleIncorrectXSDMessage()
        {
            //We found it, but could not parse it
            string BookmarkRefuge = BookmarkPath + System.IO.Path.DirectorySeparatorChar + "InvalidSchemaBookmark.xml";
            System.IO.File.Move(BookmarkFilePath, BookmarkPath + System.IO.Path.DirectorySeparatorChar + "InvalidSchemaBookmark.xml");
            System.Windows.Forms.MessageBox.Show("I could not read your bookmark.xml file, so I moved it to: \n" + BookmarkRefuge + "\n" +
                                            "You can probably recover them by closing Viking and setting/replacing the xmnls attribute on the \"root\" element to:\n" +
                                            "xmlns=\"http://tempuri.org/BookmarkSchema.xsd\" and replacing the Bookmarks.xml with it.");
        }

        /// <summary>
        /// Replaces Local XML only. Extra files stay in the session. Used by undo and the old Load path.
        /// </summary>
        private static void ReplaceLocalXml(XRoot xml)
        {
            if (xml is null)
                throw new ArgumentNullException(nameof(xml));

            RecursivelyUpdateVolumePositions(xml.Folder);

            if (_store is null)
            {
                _store = BookmarkDocumentStore.FromLocal(BookmarkFilePath, xml);
                _store.Local.Root = new FolderUIObj(null, xml.Folder, _store.Local);
            }
            else
            {
                Documents.Local.Xml = xml;
                Documents.Local.Root = new FolderUIObj(null, xml.Folder, Documents.Local);
            }

            SelectedFolder = FolderUIObjRoot;
            RaiseRootChanged();
            RaiseDocumentsChanged();
        }

        private static void InstallLocalStore(XRoot xml)
        {
            RecursivelyUpdateVolumePositions(xml.Folder);
            _store = BookmarkDocumentStore.FromLocal(BookmarkFilePath, xml);
            _store.Local.Root = new FolderUIObj(null, xml.Folder, _store.Local);
            SelectedFolder = FolderUIObjRoot;
            RaiseRootChanged();
        }

        private static void LoadSidecarExtras()
        {
            if (_store is null)
                return;

            foreach (string extraPath in Documents.ReadSidecarPaths().ToArray())
                TryLoadExtraDocument(extraPath, persistSidecar: false, showErrors: false);

            Documents.PersistSidecar();
            RaiseDocumentsChanged();
        }

        private static XRoot? TryReadUndoXml()
        {
            if (!File.Exists(BookmarkUndoFilePath))
                return null;

            return XRoot.Load(BookmarkUndoFilePath);
        }

        private static XRoot? TryLoadXml(string path, bool showErrors)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                return XRoot.Load(path);
            }
            catch (Xml.Schema.Linq.LinqToXsdException)
            {
                try
                {
                    connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot oldRoot = connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot.Load(path);
                    return MigrateV1ToV2.Migrate(path);
                }
                catch (Exception ex)
                {
                    if (showErrors)
                        MessageBox.Show("Could not parse bookmark XML: " + ex.Message);
                    return null;
                }
            }
            catch (Exception ex)
            {
                if (showErrors)
                    MessageBox.Show("Could not parse bookmark XML: " + ex.Message);
                return null;
            }
        }

        private static void RaiseRootChanged()
        {
            if (RootBookmarkChanged is null)
                return;

            if (Viking.UI.State.MainThreadDispatcher is not null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(
                    RootBookmarkChanged,
                    [null!, new System.ComponentModel.PropertyChangedEventArgs("FolderUIObjRoot")]);
            }
            else
            {
                RootBookmarkChanged(null!, new System.ComponentModel.PropertyChangedEventArgs("FolderUIObjRoot"));
            }
        }

        private static void RaiseDocumentsChanged() => DocumentsChanged?.Invoke(null, EventArgs.Empty);

        private static bool IsUnder(FolderUIObj? folder, FolderUIObj? ancestor)
        {
            while (folder != null)
            {
                if (ReferenceEquals(folder, ancestor))
                    return true;
                folder = folder.Parent;
            }

            return false;
        }

        /// <summary>
        /// Recursively update all bookmark positions with the new transform
        /// </summary>
        public static void RecursivelyUpdateVolumePositions(Folder folder)
        {
            if (folder is null || Viking.UI.State.volume is null)
                return;

            foreach (var bookmark in folder.Bookmarks)
            {
                Viking.VolumeModel.IVolumeToSectionTransform transform = Viking.UI.State.volume.GetSectionToVolumeTransform((int)bookmark.Z);
                if (transform.TrySectionToVolume(bookmark.MosaicPosition.ToVector2(), out Vector2 sectionPosition))
                {
                    bookmark.VolumePosition = new Point2D(sectionPosition);
                }
            }

            foreach (var subfolder in folder.Folders)
            {
                RecursivelyUpdateVolumePositions(subfolder);
            }
        }

        /// <summary>
        /// When this occurs we should update the positions we draw the locations at.
        /// </summary>
        public static void OnVolumeTransformChanged(object sender, TransformChangedEventArgs e)
        {
            if (_store is null)
                return;

            foreach (BookmarkDocument document in Documents.Documents)
                RecursivelyUpdateVolumePositions(document.Folder);
        }

        #endregion
    }
}
