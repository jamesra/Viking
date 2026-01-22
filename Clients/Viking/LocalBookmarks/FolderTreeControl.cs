using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI.BaseClasses;
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
            Global.RootBookmarkChanged += this.OnRootChanged;
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

        public void OnRootChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "FolderUIObjRoot")
            {
                this.SetRootFolder(Global.FolderUIObjRoot);
            }
        }

        public void SetRootFolder(FolderUIObj root)
        {
            Tree.ClearObjects();

            if (root != null)
            {
                List<IUIObject> TreeObjectList = [.. root.Folders, .. root.Bookmarks];

                Tree.AddObjects([.. TreeObjectList]);

                Global.FolderUIObjRoot.ChildChanged += OnRootChildChanged;
            }
        }

        protected void OnRootChildChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    Tree.ClearObjects();
                    break;
                case NotifyCollectionChangedAction.Add:
                    this.Tree.AddObjects(e.NewItems.Cast<IUIObject>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (object obj in e.OldItems)
                    {
                        if (obj is not IUIObject UIObj)
                            continue;

                        GenericTreeNode[] nodes = this.Tree.GetNodesForObject(UIObj);
                        foreach (GenericTreeNode node in nodes)
                        {
                            if (node.Parent is null)
                                this.Tree.RemoveNode(node);
                        }
                    }

                    break;
            }
        }

        protected override void InitializeTree()
        {
            //SetRootFolder(Global.FolderUIObjRoot);
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

                ToolStripMenuItem ImportMenu = new("Import");
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
            OpenFileDialog fileDialog = new()
            {
                DefaultExt = ".xml",
                Title = "Import Bookmark XML File",
                CheckFileExists = true,
                AddExtension = true,
                AutoUpgradeEnabled = true,
                Multiselect = false
            };

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                Global.FolderUIObjRoot.ChildChanged -= OnRootChildChanged;
                Global.Load(fileDialog.FileName);

                //Tree should be initialized by root change event
            }
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
                folder.Name = e.Label is null || e.Label.Length == 0 ? "Unnamed" : e.Label;

                folder.Save();
                return;
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy; // Okay
            else
                base.OnDragEnter(e);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] formats = e.Data.GetFormats();
                string filename = e.Data.GetData(typeof(string)) as string;

                Global.FolderUIObjRoot.ChildChanged -= OnRootChildChanged;
                Global.Load(filename);

                InitializeTree();
            }
            else
            {
                base.OnDragDrop(e);
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy; // Okay
            else
                base.OnDragEnter(e);
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

                    ToolStripMenuItem menuItem = new("New");
                    menuItem.Click += ContextMenuOnNewRootFolder;
                    menu.Items.Add(menuItem);

                    menu.Show(this, e.Location);
                }
            }
        }

        private void FolderTreeControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy; // Okay
            else
                base.OnDragEnter(e);
        }




    }
}
