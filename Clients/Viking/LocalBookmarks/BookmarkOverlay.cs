using System;
using System.Linq;
using System.Windows.Forms;
using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.Common;
using Viking.UI;
using Viking.UI.Controls;
using VikingXNA;
using VikingXNAGraphics;

namespace LocalBookmarks
{
    [SectionOverlay("Local Bookmarks")]
    class BookmarkOverlay : ISectionOverlayExtension, IProvideContextMenus
    {
        #region XNA

        protected TransformChangedEventHandler VolumeTransformChangedEventHandler;


        public static Texture2D StarTexture;
        public static Texture2D RingTexture;
        public static Texture2D ArrowTexture;
        public static Texture2D DefaultTexture;

        public static readonly VertexPositionColorTexture[] SquareVerts = [
            new(new Vector3(-1,1,0), Color.White, Vector2.Zero),
            new(new Vector3(1,1,0), Color.White, Vector2.UnitX),
            new(new Vector3(-1,-1,0), Color.White, Vector2.UnitY),
            new(new Vector3(1,-1,0), Color.White, Vector2.One) ];

        public static readonly int[] SquareIndicies = [2, 1, 0, 3, 1, 2];

        public static VertexDeclaration? VertexPositionColorTextureDecl = null;

        #endregion

        #region ISectionOverlayExtension Members

        private SectionViewerControl _parent;

        public BookmarkOverlay()
        {
            VolumeTransformChangedEventHandler = Global.OnVolumeTransformChanged;
        }

        string ISectionOverlayExtension.Name() => "Bookmarks";

        int ISectionOverlayExtension.DrawOrder() => 5;

        void ISectionOverlayExtension.SetParent(SectionViewerControl parent)
        {
            _parent = parent;
            StarTexture = parent.Content.Load<Texture2D>("Star");
            RingTexture = parent.Content.Load<Texture2D>("Ring");
            ArrowTexture = parent.Content.Load<Texture2D>("Arrow");

            DefaultTexture = StarTexture;

            State.volume.TransformChanged += VolumeTransformChangedEventHandler;

            Global.FolderUIObjRoot = new FolderUIObj(null, Global.FolderRoot);
            Global.SelectedFolder = Global.FolderUIObjRoot;
        }

        object ISectionOverlayExtension.ObjectAtPosition(GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue;
            return RecursiveFindBookmarks(Global.FolderUIObjRoot, WorldPosition, ref distance);
        }

        BookmarkUIObj RecursiveFindBookmarks(FolderUIObj parentFolder, GridVector2 position, ref double nearestDistance)
        {
            BookmarkUIObj nearestBookmark = null;
            foreach (BookmarkUIObj bookmark in parentFolder.Bookmarks)
            {
                if (State.ViewerControl.Section.Number == bookmark.Z)
                {
                    double bookmarkDistance = GridVector2.Distance(position, bookmark.GridPosition);
                    if (bookmarkDistance < nearestDistance && bookmarkDistance < Global.DefaultBookmarkRadius)
                    {
                        nearestDistance = bookmarkDistance;
                        nearestBookmark = bookmark;
                    }
                }
            }

            //Walk the bookmark tree and draw every bookmark
            foreach (FolderUIObj folder in parentFolder.Folders)
            {
                double childDistance = double.MaxValue;
                BookmarkUIObj nearestChildBookmark = RecursiveFindBookmarks(folder, position, ref childDistance);
                if (childDistance < nearestDistance)
                {
                    nearestDistance = childDistance;
                    nearestBookmark = nearestChildBookmark;
                }
            }

            return nearestBookmark;
        }

        private static BasicEffect basicEffect;
        void ISectionOverlayExtension.Draw(GraphicsDevice graphicsDevice, Scene scene, Texture BackgroundLuma, Texture BackgroundColors, ref int nextStencilValue)
        {

            basicEffect ??= new BasicEffect(graphicsDevice);

            if (basicEffect.IsDisposed)
                basicEffect = new BasicEffect(graphicsDevice);

            basicEffect.World = scene.World;
            basicEffect.Projection = scene.Projection;
            basicEffect.View = scene.Camera.View;

            basicEffect.FogEnabled = false;
            basicEffect.LightingEnabled = false;

            RecursiveDrawBookmarks(Global.FolderUIObjRoot, graphicsDevice, basicEffect, scene);
        }

        void RecursiveDrawBookmarks(FolderUIObj ParentFolder,
                                    GraphicsDevice graphicsDevice,
                                    BasicEffect basicEffect,
                                    Scene scene)
        {
            BookmarkUIObj[] bookmarks = [.. ParentFolder.Bookmarks.Where(b => b.Z == State.ViewerControl.Section.Number && scene.VisibleWorldBounds.Intersects(b.BoundingRect))];

            _parent.AnnotationOverlayEffect.Technique = OverlayShaderEffect.Techniques.SingleColorTextureLumaOverlayEffect;
            TextureOverlayView.Draw(graphicsDevice, scene, _parent.AnnotationOverlayEffect, [.. bookmarks.Select(b => b.ShapeView)]);

            LabelView.Draw(_parent.spriteBatch, VikingXNAGraphics.Global.DefaultFont, scene, [.. bookmarks.Select(b => b.LabelView)]);

            foreach (FolderUIObj folder in ParentFolder.Folders)
            {
                RecursiveDrawBookmarks(folder, graphicsDevice, basicEffect, scene);
            }
        }

        #endregion

        public ContextMenuStrip BuildMenuFor(IContextMenu Obj, ContextMenuStrip Menu)
        {
            Menu ??= new ContextMenuStrip();

            // Add a default menu item
            ToolStripMenuItem addBookmarkItem = new("Add Bookmark");
            addBookmarkItem.Click += (sender, e) => State.ViewerControl.CommandQueue.EnqueueCommand(typeof(CreateBookmarkCommand), State.ViewerControl, Global.FolderUIObjRoot);
            Menu.Items.Add(addBookmarkItem);

            // If the object provides its own context menu, merge it
            if (Obj?.ContextMenu != null)
            {
                foreach (ToolStripItem item in Obj.ContextMenu.Items)
                {
                    // Create a copy of the item
                    if (item is ToolStripMenuItem menuItem)
                    {
                        Menu.Items.Add(CloneToolStripMenuItem(menuItem));
                    }
                    else if (item is ToolStripSeparator)
                    {
                        Menu.Items.Add(new ToolStripSeparator());
                    }
                }
            }

            return Menu;
        }

        private ToolStripMenuItem CloneToolStripMenuItem(ToolStripMenuItem original)
        {
            ToolStripMenuItem clone = new(original.Text)
            {
                Enabled = original.Enabled,
                Checked = original.Checked,
                Tag = original.Tag
            };

            // Clone event handlers by invoking the original handler when the new one is clicked
            clone.Click += (sender, e) => original.PerformClick();

            // Clone sub-menu items recursively
            foreach (ToolStripItem subItem in original.DropDownItems)
            {
                if (subItem is ToolStripMenuItem subMenuItem)
                {
                    clone.DropDownItems.Add(CloneToolStripMenuItem(subMenuItem));
                }
                else if (subItem is ToolStripSeparator)
                {
                    clone.DropDownItems.Add(new ToolStripSeparator());
                }
            }

            return clone;
        }

        public ContextMenuStrip BuildMenuFor(object Obj, ContextMenuStrip Menu)
        {
            if (Obj is null)
                return Menu;

            Menu ??= new ContextMenuStrip();

            if (Obj.GetType() == typeof(FolderUIObj))
            {
                ToolStripMenuItem deleteFolderItem = new("Delete Folder");
                deleteFolderItem.Click += (sender, e) =>
                {
                    // Logic to delete folder
                };
                Menu.Items.Add(deleteFolderItem);
            }
            else if (Obj.GetType() == typeof(BookmarkUIObj))
            {
                ToolStripMenuItem propertiesItem = new("Properties");
                propertiesItem.Click += (sender, e) =>
                {
                    // Logic to open bookmark
                };
                Menu.Items.Add(propertiesItem);

                ToolStripMenuItem deleteBookmarkItem = new("Delete Bookmark");
                deleteBookmarkItem.Click += (sender, e) =>
                {
                    // Logic to delete bookmark
                };
                Menu.Items.Add(deleteBookmarkItem);
            }

            return Menu;
        }

        public ContextMenuStrip BuildMenuFor(Type ObjType, ContextMenuStrip Menu)
        {
            Menu ??= new ContextMenuStrip();

            if (ObjType == typeof(FolderTreeControl))
            {
                ToolStripMenuItem createFolderItem = new("Create Folder");
                createFolderItem.Click += (sender, e) =>
                {
                    Folder newFolder = new()
                    {
                        Name = "New Folder"
                    };
                    FolderUIObj newFolderUIObj = new(Global.FolderUIObjRoot, newFolder);
                };
                Menu.Items.Add(createFolderItem);
            }
            else if (ObjType == typeof(BookmarkUIObj))
            {
                ToolStripMenuItem openBookmarkItem = new("Open Bookmark");
                openBookmarkItem.Click += (sender, e) =>
                {
                    // Logic to open bookmark
                };
                Menu.Items.Add(openBookmarkItem);
                ToolStripMenuItem deleteBookmarkItem = new("Delete Bookmark");
                deleteBookmarkItem.Click += (sender, e) =>
                {
                    // Logic to delete bookmark
                };
                Menu.Items.Add(deleteBookmarkItem);
            }

            return Menu;
        }
    }
}
