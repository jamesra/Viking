using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Windows.Forms;

namespace LocalBookmarks
{
    [Viking.Common.Command()]    
    class CreateBookmarkCommand : Viking.UI.Commands.Command
    {
        GridVector2 bookmarkPosition;
        FolderUIObj ParentFolder;

        VikingXNAGraphics.TextureCircleView circleView;

        public CreateBookmarkCommand(Viking.UI.Controls.SectionViewerControl parent, FolderUIObj parentFolder)
            : base(parent)
        {
            //Make the cursor something distinct and appropriate for measuring
            parent.Cursor = Cursors.Cross;
            //Cursor.Hide();

            ParentFolder = parentFolder; 

            this.CommandActive = true;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            this.CommandActive = true;
            base.OnMouseMove(sender,e);

            Parent.Invalidate();
        }

        protected override void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);

            //Figure out if we are starting a rectangle
            if (e.Button == MouseButtons.Left)
            {
                bookmarkPosition = Parent.ScreenToWorld(e.X, e.Y);
                Execute(); 
            }

            base.OnMouseDown(sender, e);
        }

        protected override void Execute()
        {
            BookmarkUIObj bookmark = new BookmarkUIObj(ParentFolder);
            System.Drawing.Point ControlPostion = Viking.UI.State.ViewerForm.PointToClient(Cursor.Position);
            GridVector2 WorldPosition = Viking.UI.State.ViewerControl.ScreenToWorld(ControlPostion.X, ControlPostion.Y);

            Viking.VolumeModel.IVolumeToSectionTransform mapping = Parent.Section.ActiveSectionToVolumeTransform;

            GridVector2 SectionPosition;
            bool mappedToSection = mapping.TryVolumeToSection(WorldPosition, out SectionPosition);

            bookmark.X = WorldPosition.X;
            bookmark.Y = WorldPosition.Y;
            bookmark.Z = Viking.UI.State.ViewerControl.Section.Number;
            bookmark.Downsample = Viking.UI.State.ViewerControl.Downsample;
            bookmark.Name = "X:" + bookmark.X.ToString("F0") +
                            " Y:" + bookmark.Y.ToString("F0") +
                            " Z:" + bookmark.Z.ToString();

            if(mappedToSection)
            {
                bookmark.MosaicPosition = new connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd.Point2D(SectionPosition);
            }

            bookmark.Save(); 

            base.Execute();
        }

        protected override void OnMouseLeave(object sender, EventArgs e)
        {
            Cursor.Show();
            this.CommandActive = false;
            Parent.Invalidate();
            base.OnMouseLeave(sender, e);
        }

        protected override void OnMouseEnter(object sender, EventArgs e)
        {
            Cursor.Hide();
            this.CommandActive = true;
            Parent.Invalidate();
            base.OnMouseEnter(sender, e);
        }

        protected override void OnDeactivate()
        {
            Cursor.Show();
            base.OnDeactivate();
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        { 
            circleView = new VikingXNAGraphics.TextureCircleView(BookmarkOverlay.StarTexture, new GridCircle(this.oldWorldPosition, Global.DefaultBookmarkRadius), Microsoft.Xna.Framework.Color.Gold);
            circleView.Alpha = (float)(DateTime.UtcNow.Second % 6) / 6f;  

            if(circleView != null)
            {
                VikingXNAGraphics.TextureCircleView.Draw(graphicsDevice, scene, basicEffect, Parent.AnnotationOverlayEffect, new VikingXNAGraphics.CircleView[] { circleView });
            }
            /*
            BookmarkOverlay.DrawCircle(graphicsDevice, basicEffect, this.oldWorldPosition, Global.DefaultBookmarkRadius * scene.Camera.Downsample, 
                new Microsoft.Xna.Framework.Color(Microsoft.Xna.Framework.Color.Gold.R, 
                                                  Microsoft.Xna.Framework.Color.Gold.G,
                                                  Microsoft.Xna.Framework.Color.Gold.B,
                                                  0.5f));
                                                  */
        }
    }
}
