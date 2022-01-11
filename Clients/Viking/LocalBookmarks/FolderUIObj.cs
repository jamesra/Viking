using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;
using Viking.UI.Controls;
using VikingXNAGraphics;
 
namespace LocalBookmarks
{

    public enum ShapeType
    {
        RING,
        ARROW,
        STAR,
        INHERIT
    }

    [Viking.Common.UI.TreeViewVisible()]
    partial class FolderUIObj : UIObjTemplate<Folder>
    {
        public FolderUIObj(FolderUIObj parent)
        {
            Data = new Folder();
            Parent = parent;

            this.CallOnCreate();
        }

        public FolderUIObj(FolderUIObj parent, Folder folder)
        {
            Data = folder;
            _Parent = parent;
        }

        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            if (OnCreate != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
            }
        }
        public static event EventHandler Create
        {
            add { OnCreate += value; }
            remove { OnCreate -= value; }
        }


        private List<FolderUIObj> _Folders = null;
        [Viking.Common.UI.ThisToManyRelationAttribute()]
        public FolderUIObj[] Folders
        {
            get
            {
                if (_Folders == null)
                {
                    _Folders = new List<FolderUIObj>(Data.Folders.Count);
                    foreach (Folder folder in Data.Folders)
                    {
                        FolderUIObj child = new FolderUIObj(this, folder);
                        _Folders.Add(child);
                    }
                }

                return _Folders.ToArray();
            }
        }

        private List<BookmarkUIObj> _Bookmarks = null;
        [Viking.Common.UI.ThisToManyRelationAttribute()]
        public BookmarkUIObj[] Bookmarks
        {
            get
            {
                if (_Bookmarks == null)
                {
                    _Bookmarks = new List<BookmarkUIObj>(Data.Bookmarks.Count);
                    foreach (Bookmark bookmark in Data.Bookmarks)
                    {
                        BookmarkUIObj child = new BookmarkUIObj(this, bookmark);
                        _Bookmarks.Add(child);
                    }
                }

                return _Bookmarks.ToArray();
            }
        }

        public override string Name
        {
            get { return Data.Name; }
            set
            {
                Data.Name = value;
                if (Data.Name == null)
                    Data.Name = "";
                ValueChangedEvent("Name");
            }
        }

        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = base.ContextMenu;

                MenuItem PlaceBookmarkMenu = new MenuItem("Place Bookmark...", new EventHandler(OnPlaceBookmark));
                menu.MenuItems.Add(0, PlaceBookmarkMenu);

                MenuItem NewFolderMenu = new MenuItem("New Folder...", new EventHandler(OnNewFolder));
                menu.MenuItems.Add(1, NewFolderMenu);

                //       MenuItem ImportMenu = new MenuItem("Import...", new EventHandler(OnImportXML));
                //       menu.MenuItems.Add(2, ImportMenu);

                MenuItem ExportMenu = new MenuItem("Export");
                menu.MenuItems.Add(2, ExportMenu);

                MenuItem ExportHTMLMenu = new MenuItem("HTML...", new EventHandler(OnExportHTML));
                ExportMenu.MenuItems.Add(ExportHTMLMenu);

                MenuItem ExportXMLMenu = new MenuItem("XML...", new EventHandler(OnExportXML));
                ExportMenu.MenuItems.Add(ExportXMLMenu);

                MenuItem ImportMenu = new MenuItem("Import", new EventHandler(OnImportXML));
                menu.MenuItems.Add(3, ImportMenu);

                return menu;
            }
        }

        public ShapeType Shape
        {
            get
            {
                return Data.Shape.ToShape();
            }

            set
            {
                Data.Shape = value.ToShapeString();
                ValueChangedEvent("Shape");
                UpdateChildViews();
            }
        }

        public Microsoft.Xna.Framework.Graphics.Texture2D ShapeTexture
        {
            get
            {
                if (Shape == ShapeType.INHERIT)
                {
                    if (Parent == null)
                    {
                        return BookmarkOverlay.DefaultTexture;
                    }

                    return Parent.ShapeTexture;
                }

                return Shape.ToTexture();
            }

        }


        private Microsoft.Xna.Framework.Color? _Color = new Microsoft.Xna.Framework.Color?();

        public Microsoft.Xna.Framework.Color Color
        {
            get
            {
                if (_Color.HasValue)
                {
                    return _Color.Value;
                }

                if (Data.Color == null)
                {
                    if (Parent == null)
                    {
                        return Global.DefaultColor;
                    }
                    else
                    {
                        return Parent.Color;
                    }
                }

                try
                {
                    var gColor = Geometry.Graphics.Color.FromInteger(Data.Color);
                    _Color = new Microsoft.Xna.Framework.Color((int)gColor.R, (int)gColor.G, (int)gColor.B, (int)gColor.A);
                    return _Color.Value;
                }
                catch (FormatException)
                {
                    System.Diagnostics.Trace.WriteLine("Could not parse color: " + Data.Color);
                    return Global.DefaultColor;
                }
            }
            set
            {
                if (value == null)
                {
                    Data.Color = null;
                    _Color = new Microsoft.Xna.Framework.Color();
                }

                Data.Color = value.ToHexString();
                _Color = value;
                ValueChangedEvent("Color");
                UpdateChildViews();
            }
        }

        private void UpdateChildViews()
        {
            foreach (BookmarkUIObj bookmark in this.Bookmarks)
            {
                bookmark.UpdateView();
            }

            foreach (FolderUIObj folder in this.Folders)
            {
                folder.UpdateChildViews();
            }
        }


        /// <summary>
        /// Don't call these from controls, they are helpers
        /// </summary>
        /// <param name="child"></param>
        internal void AddChild(object child)
        {
            FolderUIObj childFolder = child as FolderUIObj;
            if (childFolder != null)
            {
                if (false == Folders.Contains(childFolder))
                    _Folders.Add(childFolder);
                if (false == Data.Folders.Contains(childFolder.Data))
                    Data.Folders.Add(childFolder.Data);
            }

            BookmarkUIObj childBookmark = child as BookmarkUIObj;
            if (childBookmark != null)
            {
                if (false == Bookmarks.Contains(childBookmark))
                    _Bookmarks.Add(childBookmark);
                if (false == Data.Bookmarks.Contains(childBookmark.Data))
                    Data.Bookmarks.Add(childBookmark.Data);
            }

            CallOnChildChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, child));
        }


        /// <summary>
        /// Don't call these from controls, they are helpers
        /// </summary>
        /// <param name="child"></param>
        internal void RemoveChild(object child)
        {
            FolderUIObj childFolder = child as FolderUIObj;
            if (childFolder != null)
            {
                Data.Folders.Remove(childFolder.Data);
                _Folders.Remove(childFolder);
            }

            BookmarkUIObj childBookmark = child as BookmarkUIObj;
            if (childBookmark != null)
            {
                Data.Bookmarks.Remove(childBookmark.Data);
                _Bookmarks.Remove(childBookmark);
            }

            CallOnChildChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, child));
        }

        #region IUIObject Members



        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            GenericTreeNode node = new GenericTreeNode(this);
            node.Name = this.Name;
            return node;
        }

        public override int TreeImageIndex
        {
            get
            {
                return 0;
            }
        }

        public override int TreeSelectedImageIndex
        {
            get
            {
                return 1;
            }
        }

        #endregion

        #region IUIObjectBasic Members

        public override string ToolTip
        {
            get { return Data.Name; }
        }


        public override void Delete()
        {
            CallBeforeDelete();
            Parent.RemoveChild(this);
            //   Parent.Data.Folders.Remove(this.Data);
            CallAfterDelete();
            Global.Save();
        }

        #endregion

        /// <summary>
        /// Create a command allowing the user to place a bookmark in this folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnPlaceBookmark(object sender, EventArgs e)
        {
            Viking.UI.State.ViewerControl.CommandQueue.EnqueueCommand(typeof(CreateBookmarkCommand), new object[]{ Viking.UI.State.ViewerControl,
                                                                                                    this});
        }

        #region Context Menu

        /// <summary>
        /// Create a command allowing the user to place a bookmark in this folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnNewFolder(object sender, EventArgs e)
        {
            FolderUIObj newFolder = new FolderUIObj(this);
            newFolder.Name = "New Folder";

            newFolder.Save();
        }

        protected void OnImportXML(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.DefaultExt = ".xml";
            fileDialog.FileName = this.Name;
            fileDialog.AddExtension = true;
            fileDialog.AutoUpgradeEnabled = true;
            fileDialog.CheckFileExists = true;
            fileDialog.Multiselect = false;
            fileDialog.Title = "Import Bookmark XML File";

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                Folder newFolder = Folder.Load(fileDialog.FileName);
                foreach (Folder f in newFolder.Folders)
                {
                    FolderUIObj newFolderUI = new FolderUIObj(this, f);
                    this.AddChild(newFolderUI);
                }

                foreach (Bookmark b in newFolder.Bookmarks)
                {
                    BookmarkUIObj newBookmarkUI = new BookmarkUIObj(this, b);
                    this.AddChild(newBookmarkUI);
                }

                //ExportXML(fileDialog.FileName);
                this.Data.Save(fileDialog.FileName);
            }
        }

        public void ExportHTML(string Filename)
        {
            HTMLExporter exporter = new HTMLExporter(this);
            exporter.WriteHTML(Filename);
        }

        protected void OnExportHTML(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.AutoUpgradeEnabled = true;
            fileDialog.DefaultExt = ".html";
            fileDialog.FileName = this.Name;
            fileDialog.OverwritePrompt = true;
            fileDialog.Title = "Export Bookmark HTML File";

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                ExportHTML(fileDialog.FileName);
            }
        }

        protected void OnExportXML(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.AutoUpgradeEnabled = true;
            fileDialog.DefaultExt = ".xml";
            fileDialog.FileName = this.Name;
            fileDialog.OverwritePrompt = true;
            fileDialog.Title = "Export Bookmark XML File";

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                //ExportXML(fileDialog.FileName);
                this.Data.Save(fileDialog.FileName);
            }
        }

        #endregion

        public void ImportXML(string XMLFile)
        {
            XRoot BookmarkXMLDoc;

            try
            {
                BookmarkXMLDoc = XRoot.Load(XMLFile);
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not parse provided XML File: " + e.ToString());
                return;
            }

            //Walk the new XML and insert it into our nodes


        }

    }
}
