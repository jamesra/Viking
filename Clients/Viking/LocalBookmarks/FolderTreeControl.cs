using System;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI.Controls;

namespace LocalBookmarks
{
    [Viking.Common.ExtensionTab("Bookmarks", Viking.Common.TABCATEGORY.ACTION)]
    [Viking.Common.SupportedUITypes([typeof(FolderUIObj), typeof(BookmarkUIObj), typeof(string)])]
    class FolderTreeControl : Viking.UI.BaseClasses.DockingTreeControl, IContextMenu
    {
        private ImageList imageList;
        private System.ComponentModel.IContainer components;

        public FolderTreeControl() : base()
        {
            BookmarkUIObj.Create += OnCreate;
            FolderUIObj.Create += OnCreate;
            this.Title = "Bookmarks";

            InitializeComponent();

            Global.AfterUndo += this.OnAfterUndo;
            Global.DocumentsChanged += this.OnDocumentsChanged;

            this.Tree.TryHandleExternalDrag = OnTreeExternalDrag;
            this.Tree.TryHandleExternalDrop = OnTreeExternalDrop;
        }

        protected void OnCreate(object sender, EventArgs e)
        {
            GenericTreeNode[] nodes = this.Tree.GetNodesForObject(sender as IUIObject);
            if (nodes is null)
                return;

            if (nodes.Length > 0)
            {
                GenericTreeNode node = nodes[0];
                Tree.SelectedNode = node;
                node.BeginEdit();
            }
        }

        protected void OnAfterUndo(object sender, EventArgs e)
        {
            this.Tree.Nodes.Clear();
            this.InitializeTree();
        }

        public void OnDocumentsChanged(object? sender, EventArgs e) => RebuildDocumentTree();

        /// <summary>
        /// Shows each loaded file as a top-level node (Local first). Nested folders hang under those roots.
        /// </summary>
        public void RebuildDocumentTree()
        {
            Tree.ClearObjects();

            if (!Global.HasDocuments)
                return;

            Tree.AddObjects(Global.Documents.Documents
                .Select(document => document.Root)
                .Where(root => root != null)
                .Cast<IUIObject>());
        }

        protected override void InitializeTree()
        {
            RebuildDocumentTree();
        }

        /// <summary>
        /// Called when the selected node is null
        /// </summary>
        public ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip CMenu = new();

                ToolStripMenuItem bookmarkItem = new("Place Bookmark...");
                bookmarkItem.Click += ContextMenuOnNewRootBookmark;
                CMenu.Items.Add(bookmarkItem);

                ToolStripMenuItem folderItem = new("New Folder");
                folderItem.Click += ContextMenuOnNewRootFolder;
                CMenu.Items.Add(folderItem);

                ToolStripMenuItem ExportMenu = new("Export");
                CMenu.Items.Add(ExportMenu);

                ToolStripMenuItem ExportHTMLMenu = new("HTML...");
                ExportHTMLMenu.Click += ContextMenuOnExportHTML;
                ExportMenu.DropDownItems.Add(ExportHTMLMenu);

                ToolStripMenuItem ExportXMLMenu = new("XML...");
                ExportXMLMenu.Click += ContextMenuOnExportXML;
                ExportMenu.DropDownItems.Add(ExportXMLMenu);

                ToolStripMenuItem ImportMenu = new("Open File...");
                ImportMenu.Click += ContextMenuOnImportRootFolder;
                CMenu.Items.Add(ImportMenu);
                return CMenu;
            }
        }

        /// <summary>
        /// create a new folder at the root level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuOnNewRootFolder(object sender, EventArgs e)
        {
            FolderUIObj newFolder = new(Global.FolderUIObjRoot)
            {
                Name = "New Folder"
            };
            newFolder.Save();
        }

        /// <summary>
        /// create a new bookmark at the root level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuOnNewRootBookmark(object sender, EventArgs e)
        {
            Viking.UI.State.ViewerControl.CommandQueue.EnqueueCommand(typeof(CreateBookmarkCommand), [ Viking.UI.State.ViewerControl,
                                                                                                    Global.FolderUIObjRoot]);
        }

        /// <summary>
        /// create a new folder at the root level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuOnExportHTML(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new()
            {
                DefaultExt = ".html",
                FileName = "Bookmarks",
                OverwritePrompt = true,
                Title = "Export Bookmark HTML File"
            };

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                Global.FolderUIObjRoot.ExportHTML(fileDialog.FileName);
            }
        }

        /// <summary>
        /// create a new folder at the root level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuOnExportXML(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new()
            {
                DefaultExt = ".xml",
                FileName = "Bookmarks",
                OverwritePrompt = true,
                Title = "Export Bookmark XML File"
            };

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                Global.Save(fileDialog.FileName);
            }
        }

        /// <summary>
        /// create a new folder at the root level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuOnImportRootFolder(object sender, EventArgs e)
        {
            Global.PromptAndLoadExtraDocuments();
        }

        private bool OnTreeExternalDrag(DragEventArgs e)
        {
            if (!BookmarkFileDrop.IsXmlFileDrop(e.Data))
                return false;

            e.Effect = DragDropEffects.Copy;
            return true;
        }

        private bool OnTreeExternalDrop(DragEventArgs e)
        {
            string[] paths = BookmarkFileDrop.GetDroppedXmlPaths(e.Data);
            if (paths.Length == 0)
                return false;

            foreach (string path in paths)
                Global.TryLoadExtraDocument(path);

            return true;
        }


        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new(typeof(FolderTreeControl));
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.SuspendLayout();
            // 
            // Tree
            // 
            this.Tree.ImageIndex = 0;
            this.Tree.ImageList = this.imageList;
            this.Tree.LabelEdit = true;
            this.Tree.LineColor = System.Drawing.Color.Black;
            this.Tree.SelectedImageIndex = 1;
            this.Tree.AfterLabelEdit += new System.Windows.Forms.NodeLabelEditEventHandler(this.Tree_AfterLabelEdit);
            this.Tree.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Tree_MouseDown);
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList.Images.SetKeyName(0, "folder.ico");
            this.imageList.Images.SetKeyName(1, "folder_open.ico");
            this.imageList.Images.SetKeyName(2, "Favorite_FrontFacing.ico");
            // 
            // FolderTreeControl
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Name = "FolderTreeControl";
            this.DragOver += new System.Windows.Forms.DragEventHandler(this.FolderTreeControl_DragOver);
            this.ResumeLayout(false);

        }

        private void Tree_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Node is not GenericTreeNode node)
                return;

            if (node.Tag is BookmarkUIObj bookmark)
            {
                if (e.Label is null || e.Label.Length == 0)
                {
                    return;
                }
                else if (e.Label != bookmark.Name)
                {
                    bookmark.Name = e.Label;
                    bookmark.Save();
                }

                return;
            }

            if (node.Tag is FolderUIObj folder)
            {
                if (folder.IsDocumentRoot)
                    return;

                folder.Name = e.Label is null || e.Label.Length == 0 ? "Unnamed" : e.Label;

                folder.Save();
                return;
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (BookmarkFileDrop.IsXmlFileDrop(e.Data))
                e.Effect = DragDropEffects.Copy;
            else
                base.OnDragEnter(e);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            string[] paths = BookmarkFileDrop.GetDroppedXmlPaths(e.Data);
            if (paths.Length > 0)
            {
                foreach (string path in paths)
                    Global.TryLoadExtraDocument(path);
                return;
            }

            base.OnDragDrop(e);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (BookmarkFileDrop.IsXmlFileDrop(e.Data))
                e.Effect = DragDropEffects.Copy;
            else
                base.OnDragOver(e);
        }

        private void Tree_MouseDown(object sender, MouseEventArgs e)
        {

            if (e.Button == MouseButtons.Right)
            {
                TreeNode node = Tree.GetNodeAt(e.Location);

                if (node is null)
                {
                    Viking.UI.State.SelectedObject = null;
                    ContextMenuStrip menu = new();

                    ToolStripMenuItem newFolderItem = new("New Folder");
                    newFolderItem.Click += ContextMenuOnNewRootFolder;
                    menu.Items.Add(newFolderItem);

                    ToolStripMenuItem bookmarkItem = new("Place Bookmark...");
                    bookmarkItem.Click += ContextMenuOnNewRootBookmark;
                    menu.Items.Add(bookmarkItem);

                    ToolStripMenuItem importItem = new("Open File...");
                    importItem.Click += ContextMenuOnImportRootFolder;
                    menu.Items.Add(importItem);

                    menu.Show(this, e.Location);
                }
            }
        }

        private void FolderTreeControl_DragOver(object sender, DragEventArgs e)
        {
            if (BookmarkFileDrop.IsXmlFileDrop(e.Data))
                e.Effect = DragDropEffects.Copy;
            else
                base.OnDragOver(e);
        }




    }
}
