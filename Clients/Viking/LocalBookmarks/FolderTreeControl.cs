using System;
using System.Collections.Specialized; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.UI.BaseClasses;
using Viking.Common;
using System.Windows.Forms;
using Viking.UI.Controls; 

namespace LocalBookmarks
{
    [Viking.Common.ExtensionTab("Bookmarks", Viking.Common.TABCATEGORY.ACTION)]
    [Viking.Common.SupportedUITypes(new Type[]{typeof(FolderUIObj),typeof(BookmarkUIObj), typeof(string)})]
    class FolderTreeControl : Viking.UI.BaseClasses.DockingTreeControl
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

        }

        protected void OnCreate(object sender, EventArgs e)
        {
            GenericTreeNode[] nodes = this.Tree.GetNodesForObject(sender as IUIObject);
            if (nodes == null)
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

        protected void OnRootChildChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    this.Tree.AddObjects(e.NewItems.Cast<IUIObject>());
                    break; 
                case NotifyCollectionChangedAction.Remove:
                    foreach (object obj in e.OldItems)
                    {
                        IUIObject UIObj = obj as IUIObject;
                        if (UIObj == null)
                            continue;

                        GenericTreeNode[] nodes = this.Tree.GetNodesForObject(UIObj);
                        foreach (GenericTreeNode node in nodes)
                        {
                            if (node.Parent == null)
                                this.Tree.RemoveNode(node);
                        }
                    }

                    break; 
            }
        }

        protected override void InitializeTree()
        {
            Tree.ClearObjects();

            List<IUIObject> TreeObjectList = new List<IUIObject>(Global.FolderUIObjRoot.Folders.Length + Global.FolderUIObjRoot.Bookmarks.Length);
            TreeObjectList.AddRange(Global.FolderUIObjRoot.Folders);
            TreeObjectList.AddRange(Global.FolderUIObjRoot.Bookmarks); 

            Tree.AddObjects( TreeObjectList.ToArray() );

            Global.FolderUIObjRoot.ChildChanged += OnRootChildChanged;
        }

        /// <summary>
        /// Called when the selected node is null
        /// </summary>
        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu CMenu = base.ContextMenu;
                if (CMenu == null)
                    CMenu = new ContextMenu();

                CMenu.MenuItems.Add(new TagMenuItem("Place Bookmark...", null, new EventHandler(ContextMenuOnNewRootBookmark)));
                CMenu.MenuItems.Add(new TagMenuItem("New Folder", null, new EventHandler(ContextMenuOnNewRootFolder)));
               
                MenuItem ExportMenu = new MenuItem("Export");
                CMenu.MenuItems.Add(ExportMenu);

                TagMenuItem ExportHTMLMenu = new TagMenuItem("HTML...", null, new EventHandler(ContextMenuOnExportHTML));
                ExportMenu.MenuItems.Add(ExportHTMLMenu);

                TagMenuItem ExportXMLMenu = new TagMenuItem("XML...", null, new EventHandler(ContextMenuOnExportXML));
                ExportMenu.MenuItems.Add(ExportXMLMenu);

                MenuItem ImportMenu = new TagMenuItem("Import", null, new EventHandler(ContextMenuOnImportRootFolder));
                CMenu.MenuItems.Add(ImportMenu);
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
            FolderUIObj newFolder = new FolderUIObj(Global.FolderUIObjRoot);
            newFolder.Name = "New Folder";
            newFolder.Save();
        }

        /// <summary>
        /// create a new bookmark at the root level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuOnNewRootBookmark(object sender, EventArgs e)
        {
            Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateBookmarkCommand), new object[]{ Viking.UI.State.ViewerControl, 
                                                                                                    Global.FolderUIObjRoot}); 
        }

        /// <summary>
        /// create a new folder at the root level
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuOnExportHTML(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.DefaultExt = ".html";
            fileDialog.FileName = "Bookmarks";
            fileDialog.OverwritePrompt = true;
            fileDialog.Title = "Export Bookmark HTML File"; 

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
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.DefaultExt = ".xml";
            fileDialog.FileName = "Bookmarks";
            fileDialog.OverwritePrompt = true;
            fileDialog.Title = "Export Bookmark XML File"; 

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
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.DefaultExt = ".xml";
            fileDialog.Title = "Import Bookmark XML File";
            fileDialog.CheckFileExists = true;
            fileDialog.AddExtension = true;
            fileDialog.AutoUpgradeEnabled = true;
            fileDialog.Multiselect = false; 

            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                Global.FolderUIObjRoot.ChildChanged -= OnRootChildChanged;
                Global.Load(fileDialog.FileName);

                InitializeTree(); 
            }
        }


        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FolderTreeControl));
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
            GenericTreeNode node = e.Node as GenericTreeNode;
            if (node == null)
                return;

            BookmarkUIObj bookmark = node.Tag as BookmarkUIObj;
            if (bookmark != null)
            {
                if (e.Label == null || e.Label.Length == 0)
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

            FolderUIObj folder = node.Tag as FolderUIObj;
            if (folder != null)
            {
                if (e.Label == null || e.Label.Length == 0)
                    folder.Name = "Unnamed";
                else
                    folder.Name = e.Label; 

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

        private void FolderTreeControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy; // Okay
            else
                base.OnDragEnter(e);
        }


        

    }
}
