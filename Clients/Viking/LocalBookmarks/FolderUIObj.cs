using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using Geometry.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
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
    partial class FolderUIObj : UIObjTemplate<Folder>, IContextMenu
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

        /// <summary>
        /// File-root constructor. <paramref name="document"/> makes this node Local or a filename; it is not a nested folder.
        /// </summary>
        public FolderUIObj(FolderUIObj? parent, Folder folder, BookmarkDocument document)
        {
            Data = folder;
            _Parent = parent;
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Set only on top-level Local / extra-file nodes. Nested folders leave this null.
        /// </summary>
        internal BookmarkDocument? Document { get; }

        internal bool IsDocumentRoot => Document != null;

        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            if (OnCreate != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, [this, null!]);
            }
        }
        public static event EventHandler Create
        {
            add => OnCreate += value;
            remove => OnCreate -= value;
        }


        private List<FolderUIObj>? _Folders = null;
        [Viking.Common.UI.ThisToManyRelationAttribute()]
        public FolderUIObj[] Folders
        {
            get
            {
                if (_Folders is null)
                {
                    _Folders = new List<FolderUIObj>(Data.Folders.Count);
                    foreach (Folder folder in Data.Folders)
                    {
                        FolderUIObj child = new(this, folder);
                        _Folders.Add(child);
                    }
                }

                return [.. _Folders];
            }
        }

        private List<BookmarkUIObj>? _Bookmarks = null;
        [Viking.Common.UI.ThisToManyRelationAttribute()]
        public BookmarkUIObj[] Bookmarks
        {
            get
            {
                if (_Bookmarks is null)
                {
                    _Bookmarks = new List<BookmarkUIObj>(Data.Bookmarks.Count);
                    foreach (Bookmark bookmark in Data.Bookmarks)
                    {
                        BookmarkUIObj child = new(this, bookmark);
                        _Bookmarks.Add(child);
                    }
                }

                return [.. _Bookmarks];
            }
        }

        public override string Name
        {
            get
            {
                if (IsDocumentRoot && Global.HasDocuments)
                    return Global.Documents.DisplayNameOf(Document!);

                return Data.Name;
            }
            set
            {
                if (IsDocumentRoot)
                    return;

                Data.Name = value;
                Data.Name ??= "";
                ValueChangedEvent("Name");
            }
        }

        public override System.Windows.Forms.ContextMenuStrip ContextMenu
        {
            get
            {
                if (IsDocumentRoot)
                    return BuildDocumentRootMenu();

                ContextMenuStrip menu = base.ContextMenu;

                ToolStripMenuItem PlaceBookmarkMenu = new("Place Bookmark...");
                PlaceBookmarkMenu.Click += OnPlaceBookmark;
                menu.Items.Insert(0, PlaceBookmarkMenu);

                ToolStripMenuItem NewFolderMenu = new("New Folder...");
                NewFolderMenu.Click += OnNewFolder;
                menu.Items.Insert(1, NewFolderMenu);

                ToolStripMenuItem ExportMenu = new("Export");
                menu.Items.Insert(2, ExportMenu);

                ToolStripMenuItem ExportHTMLMenu = new("HTML...");
                ExportHTMLMenu.Click += OnExportHTML;
                ExportMenu.DropDownItems.Add(ExportHTMLMenu);

                ToolStripMenuItem ExportXMLMenu = new("XML...");
                ExportXMLMenu.Click += OnExportXML;
                ExportMenu.DropDownItems.Add(ExportXMLMenu);

                ToolStripMenuItem ImportMenu = new("Import into folder...");
                ImportMenu.Click += OnImportXML;
                menu.Items.Insert(3, ImportMenu);

                return menu;
            }
        }

        private ContextMenuStrip BuildDocumentRootMenu()
        {
            ContextMenuStrip menu = new();

            ToolStripMenuItem PlaceBookmarkMenu = new("Place Bookmark...");
            PlaceBookmarkMenu.Click += OnPlaceBookmark;
            menu.Items.Add(PlaceBookmarkMenu);

            ToolStripMenuItem NewFolderMenu = new("New Folder...");
            NewFolderMenu.Click += OnNewFolder;
            menu.Items.Add(NewFolderMenu);

            ToolStripMenuItem ExportMenu = new("Export");
            menu.Items.Add(ExportMenu);

            ToolStripMenuItem ExportHTMLMenu = new("HTML...");
            ExportHTMLMenu.Click += OnExportHTML;
            ExportMenu.DropDownItems.Add(ExportHTMLMenu);

            ToolStripMenuItem ExportXMLMenu = new("XML...");
            ExportXMLMenu.Click += OnExportThisDocument;
            ExportMenu.DropDownItems.Add(ExportXMLMenu);

            ToolStripMenuItem OpenFileMenu = new("Open File...");
            OpenFileMenu.Click += (_, _) => Global.PromptAndLoadExtraDocuments();
            menu.Items.Add(OpenFileMenu);

            if (Document is { IsLocal: false })
            {
                ToolStripMenuItem removeMenu = new("Remove");
                removeMenu.Click += (_, _) => Global.TryUnloadDocument(Document);
                menu.Items.Add(removeMenu);
            }

            ToolStripMenuItem propertiesMenu = new("Properties...");
            propertiesMenu.Click += OnPropertiesClick;
            menu.Items.Add(propertiesMenu);

            return menu;
        }

        public ShapeType Shape
        {
            get => Data.Shape.ToShape();

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
                    if (Parent is null)
                    {
                        return BookmarkOverlay.DefaultTexture;
                    }

                    return Parent.ShapeTexture;
                }

                return Shape.ToTexture();
            }

        }


        private Microsoft.Xna.Framework.Color? _Color = null;

        public Microsoft.Xna.Framework.Color Color
        {
            get
            {
                if (_Color.HasValue)
                {
                    return _Color.Value;
                }

                if (Data.Color is null)
                {
                    if (Parent is null)
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
                    Color gColor = Geometry.Graphics.Color.FromInteger(Data.Color);
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
            if (child is FolderUIObj childFolder)
            {
                if (false == Folders.Contains(childFolder))
                    _Folders.Add(childFolder);
                if (false == Data.Folders.Contains(childFolder.Data))
                    Data.Folders.Add(childFolder.Data);
            }

            if (child is BookmarkUIObj childBookmark)
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
            if (child is FolderUIObj childFolder)
            {
                Data.Folders.Remove(childFolder.Data);
                _Folders.Remove(childFolder);
            }

            if (child is BookmarkUIObj childBookmark)
            {
                Data.Bookmarks.Remove(childBookmark.Data);
                _Bookmarks.Remove(childBookmark);
            }

            CallOnChildChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, child));
        }

        #region IUIObject Members



        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            GenericTreeNode node = new(this)
            {
                Name = this.Name
            };
            return node;
        }

        public override int TreeImageIndex => 0;

        public override int TreeSelectedImageIndex => 1;

        #endregion

        #region IUIObjectBasic Members

        public override string ToolTip => IsDocumentRoot ? Document!.FilePath : Data.Name;


        /// <summary>
        /// Nested folders are removed from their parent document. File roots unload extras from the session
        /// (Local is a no-op). Extra files are not deleted from disk.
        /// </summary>
        public override void Delete()
        {
            if (IsDocumentRoot)
            {
                if (Document is { IsLocal: false })
                    Global.TryUnloadDocument(Document);
                return;
            }

            CallBeforeDelete();
            Parent.RemoveChild(this);
            CallAfterDelete();
            Global.SaveOwningDocument(Parent);
        }

        public override Type[] AssignableParentTypes => IsDocumentRoot ? [] : [typeof(FolderUIObj)];

        #endregion

        /// <summary>
        /// Create a command allowing the user to place a bookmark in this folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnPlaceBookmark(object sender, EventArgs e)
        {
            Viking.UI.State.ViewerControl.CommandQueue.EnqueueCommand(typeof(CreateBookmarkCommand), [ Viking.UI.State.ViewerControl,
                                                                                                    this]);
        }

        #region Context Menu

        /// <summary>
        /// Create a command allowing the user to place a bookmark in this folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnNewFolder(object sender, EventArgs e)
        {
            FolderUIObj newFolder = new(this)
            {
                Name = "New Folder"
            };

            newFolder.Save();
        }

        protected void OnImportXML(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new()
            {
                DefaultExt = ".xml",
                FileName = this.Name,
                AddExtension = true,
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                Multiselect = false,
                Title = "Import Bookmark XML File"
            };

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                MergeFromXmlFile(fileDialog.FileName);
                Global.SaveOwningDocument(this);
            }
        }

        /// <summary>
        /// Copies folders and bookmarks from a file into this folder. Writes the owning document, never the source path.
        /// </summary>
        internal void MergeFromXmlFile(string xmlFile)
        {
            Folder? source = TryReadFolderTree(xmlFile);
            if (source is null)
                return;

            foreach (Folder f in source.Folders)
            {
                FolderUIObj newFolderUI = new(this, f);
                this.AddChild(newFolderUI);
            }

            foreach (Bookmark b in source.Bookmarks)
            {
                BookmarkUIObj newBookmarkUI = new(this, b);
                this.AddChild(newBookmarkUI);
            }
        }

        private static Folder? TryReadFolderTree(string xmlFile)
        {
            try
            {
                XRoot root = XRoot.Load(xmlFile);
                if (root.Folder != null)
                    return root.Folder;
            }
            catch (Exception)
            {
            }

            try
            {
                return Folder.Load(xmlFile);
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not parse provided XML File: " + e.ToString());
                return null;
            }
        }

        public void ExportHTML(string Filename)
        {
            HTMLExporter exporter = new(this);
            exporter.WriteHTML(Filename);
        }

        protected void OnExportHTML(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new()
            {
                AutoUpgradeEnabled = true,
                DefaultExt = ".html",
                FileName = this.Name,
                OverwritePrompt = true,
                Title = "Export Bookmark HTML File"
            };

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                ExportHTML(fileDialog.FileName);
            }
        }

        protected void OnExportXML(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new()
            {
                AutoUpgradeEnabled = true,
                DefaultExt = ".xml",
                FileName = this.Name,
                OverwritePrompt = true,
                Title = "Export Bookmark XML File"
            };

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                this.Data.Save(fileDialog.FileName);
            }
        }

        protected void OnExportThisDocument(object sender, EventArgs e)
        {
            if (Document is null)
                return;

            SaveFileDialog fileDialog = new()
            {
                AutoUpgradeEnabled = true,
                DefaultExt = ".xml",
                FileName = Path.GetFileNameWithoutExtension(Document.FilePath),
                OverwritePrompt = true,
                Title = "Export Bookmark XML File"
            };

            if (DialogResult.OK == fileDialog.ShowDialog())
                Document.Xml.Save(fileDialog.FileName);
        }

        #endregion

        public void ImportXML(string XMLFile)
        {
            MergeFromXmlFile(XMLFile);
            Global.SaveOwningDocument(this);
        }

    }
}
