using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry;
using VikingXNAGraphics;
using Viking.Common;

namespace LocalBookmarks
{
    [Viking.Common.SectionOverlay("Local Bookmarks")]
    class BookmarkOverlay : Viking.Common.ISectionOverlayExtension
    {
        #region XNA

        protected TransformChangedEventHandler VolumeTransformChangedEventHandler;
        

        static public Texture2D StarTexture;

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


            Viking.UI.State.volume.TransformChanged += VolumeTransformChangedEventHandler;
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
            float alpha = 1;
            GridRectangle Bounds = scene.VisibleWorldBounds;
            double Downsample = scene.Camera.Downsample; 
            double maxDimension = Math.Max(Bounds.Width, Bounds.Height);
            //We use the default radius unless it would be invisible.  In that case radius is scaled after calculating the alpha value
            double BookmarkRadius = Global.DefaultBookmarkRadius;

            //If the bookmark is more than 2.5% of the screen begin making it transparent
            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color(Microsoft.Xna.Framework.Color.Gold.ToVector3());
            
            double BookmarkAlphaThreshold = 0.001;
            //double BookmarkToVisibleBoundsRatio = Global.BookmarkArea / Bounds.Area;
            //if (BookmarkToVisibleBoundsRatio > BookmarkAlphaThreshold)
            double BookmarkDimensionRatio = BookmarkRadius * 2 / maxDimension; 
            if(BookmarkDimensionRatio >  BookmarkAlphaThreshold)
            {
                //A circle fills 0.78% of the screen area, so divide that by four
                double BookmarkTransparentThreshold = 0.25;

                alpha = 1f - (float)Math.Sqrt(((BookmarkDimensionRatio - BookmarkAlphaThreshold) / (BookmarkTransparentThreshold - BookmarkAlphaThreshold)));
                if (alpha < 0.00)
                    alpha = 0.00f; 
            }
                        
            double ScreenPixelRadius = BookmarkRadius / Downsample;
            
            //What is the absolute smallest size a bookmark should have?
            double minBookmarkPixelRadius = (maxDimension / Downsample) / (BookmarkRadius * Downsample) > 7.5 ? (maxDimension / Downsample) / (BookmarkRadius * Downsample) : 7.5;

            if (ScreenPixelRadius < minBookmarkPixelRadius)
            {
                BookmarkRadius = minBookmarkPixelRadius * (Downsample * (1 + (Math.Sin((DateTime.UtcNow.Millisecond / 1000.0) * Math.PI) / 10)));

                //To make it a bit more visible, vary the alpha slightly with time
                //alpha *= (float)(0.5 + (Math.Sin((DateTime.UtcNow.Millisecond / 1000.0) * Math.PI)/2)); 

            }

            color.A = (byte)(255.0 * alpha);

            
            foreach(BookmarkUIObj bookmark in ParentFolder.Bookmarks)
            {
                if (Viking.UI.State.ViewerControl.Section.Number == bookmark.Z)
                {
                    if (Bounds.Intersects(new GridRectangle(bookmark.GridPosition, Global.DefaultBookmarkRadius)))
                    {
                        DrawCircle(graphicsDevice,
                            basicEffect,
                            bookmark.GridPosition,
                            BookmarkRadius,
                            color);

                        DrawLabel(bookmark, alpha, graphicsDevice);
                    }
                    //                    bookmark.Draw(graphicsDevice, basicEffect, DownSample); 
                }
            }

            //Walk the bookmark tree and draw every bookmark
            foreach (FolderUIObj folder in ParentFolder.Folders)
            { 
                RecursiveDrawBookmarks(folder, graphicsDevice, basicEffect, scene); 
            }
        }

        static public void DrawCircle(GraphicsDevice graphicsDevice,
                BasicEffect basicEffect,
                GridVector2 Pos,
                double Radius,
                Microsoft.Xna.Framework.Color color)
        {
            //A better way to implement this is to just render a circle texture and add color using lighting, but 
            //this will work for now

            if (false == Global.BookmarksVisible)
                return; 


            VertexPositionColorTexture[] verts;

            //Can't populate until we've referenced CircleVerts
            int[] indicies;
            float radius = (float)Radius;

            //Figure out if we should draw triangles instead
            verts = new VertexPositionColorTexture[SquareVerts.Length];
            SquareVerts.CopyTo(verts, 0);

            indicies = SquareIndicies;

            //Scale and color the verticies
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Position.X *= radius;
                verts[i].Position.Y *= radius;

                verts[i].Position.X += (float)Pos.X;
                verts[i].Position.Y += (float)Pos.Y;
                verts[i].Color = color;
            }
            
            /*PORT XNA 4
            VertexDeclaration oldVertexDeclaration = graphicsDevice.VertexDeclaration;
            if (VertexPositionColorTextureDecl == null)
            {
                VertexPositionColorTextureDecl = new VertexDeclaration(graphicsDevice, VertexPositionColorTexture.VertexElements);
            }

            graphicsDevice.VertexDeclaration = VertexPositionColorTextureDecl;
            graphicsDevice.RenderState.PointSize = 5.0f;
            */

            basicEffect.Texture = StarTexture;
            basicEffect.TextureEnabled = true;
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = false;
            
            /*Fade the star if the radius is more than a set percentage of the screen*/
            //basicEffect.Alpha = 0.5f;
            //basicEffect.CommitChanges();

            //PORT XNA 4
            //basicEffect.Begin();

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                //PORT XNA 4
                //pass.Begin();
                pass.Apply(); 

                graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                              verts,
                                                                              0,
                                                                              verts.Length,
                                                                              indicies,
                                                                              0,
                                                                              2);

                //PORT XNA 4
                //pass.End();
            }

            //PORT XNA 4
            //basicEffect.End();

            /*PORT XNA 4
            //graphicsDevice.VertexDeclaration = oldVertexDeclaration;
             */
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = false;
        }

        public void DrawLabel(BookmarkUIObj bookmark, double alpha, GraphicsDevice graphicsDevice)
        {
            double VisibleLabelCutoff = 7;

            Viking.UI.Controls.SectionViewerControl parent = Viking.UI.State.ViewerControl;
         
            Vector2 LabelSize = bookmark.GetLabelSize(parent.fontArial);
            Vector2 LabelOffset = new Vector2(LabelSize.X / 2, LabelSize.Y / 2f);
            float MagnificationFactor = (1 / (float)parent.StatusMagnification) * BookmarkUIObj.LabelScaleFactor;

            
            float scale = (((float)Global.DefaultBookmarkRadius / LabelSize.X) * MagnificationFactor) / 2f;

            //Don't draw the label if it cannot be seen
            if (scale * _parent.fontArial.LineSpacing < VisibleLabelCutoff)
                return;

            GridVector2 WorldPosition = bookmark.GridPosition;
            GridVector2 DrawPosition = parent.WorldToScreen(WorldPosition.X, WorldPosition.Y);

            BlendState originalBlendState = graphicsDevice.BlendState;
            RasterizerState originalRasterState = graphicsDevice.RasterizerState;

            //Print the label
            parent.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);


            Microsoft.Xna.Framework.Color color = new Color(Color.Black.ToVector3());
            color.A = (byte)(255.0 * alpha); 

            parent.spriteBatch.DrawString(parent.fontArial,
                bookmark.Name,
                new Vector2((float)DrawPosition.X, (float)DrawPosition.Y ),
                color,
                0,
                LabelOffset,
                scale,
                SpriteEffects.None,
                0);

            parent.spriteBatch.End();

            graphicsDevice.BlendState = originalBlendState;
            graphicsDevice.RasterizerState = originalRasterState; 

            //PORT XNA 4
            //graphicsDevice.RenderState.CullMode = CullMode.None;
            //graphicsDevice.RenderState.AlphaTestEnable = false;
        }

        #endregion
    }
}
