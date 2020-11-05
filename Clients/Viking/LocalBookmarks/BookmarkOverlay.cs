using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Viking.Common;
using VikingXNAGraphics;

namespace LocalBookmarks
{
    [Viking.Common.SectionOverlay("Local Bookmarks")]
    class BookmarkOverlay : Viking.Common.ISectionOverlayExtension
    {
        #region XNA

        protected TransformChangedEventHandler VolumeTransformChangedEventHandler;


        static public Texture2D StarTexture;
        static public Texture2D RingTexture;
        static public Texture2D ArrowTexture;
        static public Texture2D DefaultTexture;

        static readonly public VertexPositionColorTexture[] SquareVerts = new VertexPositionColorTexture[] {
            new VertexPositionColorTexture(new Vector3(-1,1,0), Color.White, Vector2.Zero),
            new VertexPositionColorTexture(new Vector3(1,1,0), Color.White, Vector2.UnitX),
            new VertexPositionColorTexture(new Vector3(-1,-1,0), Color.White, Vector2.UnitY),
            new VertexPositionColorTexture(new Vector3(1,-1,0), Color.White, Vector2.One) };

        static readonly public int[] SquareIndicies = new int[] { 2, 1, 0, 3, 1, 2 };

        static public VertexDeclaration VertexPositionColorTextureDecl = null;

        #endregion

        #region ISectionOverlayExtension Members

        private Viking.UI.Controls.SectionViewerControl _parent = null;

        public BookmarkOverlay()
        {
            VolumeTransformChangedEventHandler = new TransformChangedEventHandler(Global.OnVolumeTransformChanged);
        }

        string Viking.Common.ISectionOverlayExtension.Name()
        {
            return "Bookmarks";
        }

        int Viking.Common.ISectionOverlayExtension.DrawOrder()
        {
            return 5;
        }

        void Viking.Common.ISectionOverlayExtension.SetParent(Viking.UI.Controls.SectionViewerControl parent)
        {
            _parent = parent;
            StarTexture = parent.Content.Load<Texture2D>("Star");
            RingTexture = parent.Content.Load<Texture2D>("Ring");
            ArrowTexture = parent.Content.Load<Texture2D>("Arrow");

            DefaultTexture = StarTexture;

            Viking.UI.State.volume.TransformChanged += VolumeTransformChangedEventHandler;

            Global.FolderUIObjRoot = new FolderUIObj(null, Global.FolderRoot);
            Global.SelectedFolder = Global.FolderUIObjRoot;
        }

        object Viking.Common.ISectionOverlayExtension.ObjectAtPosition(Geometry.GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue;
            return RecursiveFindBookmarks(Global.FolderUIObjRoot, WorldPosition, ref distance);
        }

        BookmarkUIObj RecursiveFindBookmarks(FolderUIObj parentFolder, GridVector2 position, ref double nearestDistance)
        {
            BookmarkUIObj nearestBookmark = null;
            foreach (BookmarkUIObj bookmark in parentFolder.Bookmarks)
            {
                if (Viking.UI.State.ViewerControl.Section.Number == bookmark.Z)
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

        static private BasicEffect basicEffect;
        void Viking.Common.ISectionOverlayExtension.Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.Texture BackgroundLuma, Microsoft.Xna.Framework.Graphics.Texture BackgroundColors, ref int nextStencilValue)
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
            return;
        }

        void RecursiveDrawBookmarks(FolderUIObj ParentFolder,
                                    Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                                    VikingXNA.Scene scene)
        {
            BookmarkUIObj[] bookmarks = ParentFolder.Bookmarks.Where(b => b.Z == Viking.UI.State.ViewerControl.Section.Number && scene.VisibleWorldBounds.Intersects(b.BoundingRect)).ToArray();

            this._parent.AnnotationOverlayEffect.Technique = OverlayShaderEffect.Techniques.SingleColorTextureLumaOverlayEffect;
            TextureOverlayView.Draw(graphicsDevice, scene, this._parent.AnnotationOverlayEffect, bookmarks.Select(b => b.ShapeView).ToArray());

            LabelView.Draw(_parent.spriteBatch, VikingXNAGraphics.Global.DefaultFont, scene, bookmarks.Select(b => b.LabelView).ToArray());

            foreach (FolderUIObj folder in ParentFolder.Folders)
            {
                RecursiveDrawBookmarks(folder, graphicsDevice, basicEffect, scene);
            }
        }

        #endregion
    }
}
