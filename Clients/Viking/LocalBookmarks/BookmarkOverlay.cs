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

        public static readonly VertexPositionColorTexture[] SquareVerts = {
            new VertexPositionColorTexture(new Vector3(-1,1,0), Color.White, Vector2.Zero),
            new VertexPositionColorTexture(new Vector3(1,1,0), Color.White, Vector2.UnitX),
            new VertexPositionColorTexture(new Vector3(-1,-1,0), Color.White, Vector2.UnitY),
            new VertexPositionColorTexture(new Vector3(1,-1,0), Color.White, Vector2.One) };

        public static readonly int[] SquareIndicies = { 2, 1, 0, 3, 1, 2 };

        public static VertexDeclaration VertexPositionColorTextureDecl = null;

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

            if (basicEffect == null)
                basicEffect = new BasicEffect(graphicsDevice);

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
            BookmarkUIObj[] bookmarks = ParentFolder.Bookmarks.Where(b => b.Z == State.ViewerControl.Section.Number && scene.VisibleWorldBounds.Intersects(b.BoundingRect)).ToArray();

            _parent.AnnotationOverlayEffect.Technique = OverlayShaderEffect.Techniques.SingleColorTextureLumaOverlayEffect;
            TextureOverlayView.Draw(graphicsDevice, scene, _parent.AnnotationOverlayEffect, bookmarks.Select(b => b.ShapeView).ToArray());

            LabelView.Draw(_parent.spriteBatch, VikingXNAGraphics.Global.DefaultFont, scene, bookmarks.Select(b => b.LabelView).ToArray());

            foreach (FolderUIObj folder in ParentFolder.Folders)
            {
                RecursiveDrawBookmarks(folder, graphicsDevice, basicEffect, scene);
            }
        }

        #endregion

        public ContextMenu BuildMenuFor(IContextMenu Obj, ContextMenu Menu)
        { 
            if (Menu == null) Menu = new ContextMenu();

            // Add a default menu item
            Menu.MenuItems.Add(new MenuItem("Add Bookmark",
                (sender, e) => {
                    State.ViewerControl.CommandQueue.EnqueueCommand(typeof(CreateBookmarkCommand), State.ViewerControl, Global.FolderUIObjRoot);
                }));

            // If the object provides its own context menu, merge it
            if (Obj?.ContextMenu != null)
                foreach (MenuItem item in Obj.ContextMenu.MenuItems)
                    Menu.MenuItems.Add(item.CloneMenu());

            return Menu;
        }

        public ContextMenu BuildMenuFor(object Obj, ContextMenu Menu)
        {
            Menu ??= new ContextMenu();

            if (Obj.GetType() == typeof(FolderUIObj))
            { 
                Menu.MenuItems.Add(new MenuItem("Delete Folder", (sender, e) =>
                {
                    // Logic to delete folder
                }));
            }
            else if (Obj.GetType() == typeof(BookmarkUIObj))
            {
                Menu.MenuItems.Add(new MenuItem("Properties", (sender, e) =>
                {
                    // Logic to open bookmark
                }));

                Menu.MenuItems.Add(new MenuItem("Delete Bookmark", (sender, e) =>
                {
                    // Logic to delete bookmark
                }));
            }

            return Menu;
        } 

        public ContextMenu BuildMenuFor(Type ObjType, ContextMenu Menu)
        {
            Menu ??= new ContextMenu();

            if (ObjType == typeof(FolderTreeControl))
            {
                Menu.MenuItems.Add(new MenuItem("Create Folder", (sender, e) =>
                {
                    Folder newFolder = new Folder();
                    newFolder.Name = "New Folder";
                    var newFolderUIObj = new FolderUIObj(Global.FolderUIObjRoot, newFolder);
                })); 
            }
            else if (ObjType == typeof(BookmarkUIObj))
            {
                Menu.MenuItems.Add(new MenuItem("Open Bookmark", (sender, e) =>
                {
                    // Logic to open bookmark
                }));
                Menu.MenuItems.Add(new MenuItem("Delete Bookmark", (sender, e) =>
                {
                    // Logic to delete bookmark
                }));
            }

            return Menu;
        }
    }
}
