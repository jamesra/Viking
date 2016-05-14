using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using WebAnnotation;
using WebAnnotationModel;
using VikingXNA;
using VikingXNAGraphics;
using Microsoft.Xna.Framework;

namespace WebAnnotation.View
{
    /// <summary>
    /// Renders labels for a Structure inside a circle
    /// </summary>
    public class StructureCircleLabels : ILabelView
    {
        public LabelView StructureIDLabelView;
        public LabelView StructureLabelView;
        public LabelView ParentStructureLabelView;
         
        LocationObj locationObj = null;

        GridCircle VolumeCircle;

        public double Radius
        {
            get { return VolumeCircle.Radius; }
            set {
                VolumeCircle.Radius = value;
                CreateLabelObjects();
            }
        }

        public StructureCircleLabels(LocationObj obj, GridCircle circle)
        {
            VolumeCircle = circle;
            locationObj = obj;
            CreateLabelObjects();
        }

        protected string StructureIDLabelWithTypeCode(StructureObj obj)
        {
            return obj.Type.Code + " " + obj.ID.ToString();
        }

        /// <summary>
        /// Full label and tag text
        /// </summary>
        /// <returns></returns>
        protected string FullLabelText()
        {
            string fullLabel = this.StructureLabel();

            if (fullLabel.Length == 0)
                fullLabel = this.TagLabel();
            else
                fullLabel += '\n' + this.TagLabel();

            return fullLabel;
        }
         
        protected string TagLabel()
        {
            string InfoLabel = "";
            foreach (ObjAttribute tag in locationObj.Parent.Attributes)
            {
                InfoLabel += tag.ToString() + " ";
            }

            foreach (ObjAttribute tag in locationObj.Attributes)
            {
                InfoLabel += tag.ToString() + " ";
            }

            return InfoLabel.Trim();
        }

        protected string StructureLabel()
        {
            string InfoLabel = "";
            if (locationObj.Parent.Label != null)
                InfoLabel = locationObj.Parent.Label.Trim();

            return InfoLabel;
        }

        private void CreateLabelObjects()
        {
            StructureIDLabelView = new LabelView(StructureIDLabelWithTypeCode(this.locationObj.Parent), this.VolumeCircle.Center - new GridVector2(0, this.VolumeCircle.Radius / 3.0f));
            StructureIDLabelView.MaxLineWidth = this.Radius * 2.0;
            StructureIDLabelView._Color = this.locationObj.IsUnverifiedTerminal ? Color.Yellow : Color.Black;

            StructureLabelView = new LabelView(this.FullLabelText(), this.VolumeCircle.Center + new GridVector2(0, this.Radius / 3.0f));
            StructureLabelView.MaxLineWidth = this.Radius * 2;


            if (locationObj.Parent.ParentID.HasValue)
            {
                ParentStructureLabelView = new LabelView(locationObj.Parent.ParentID.ToString(), this.VolumeCircle.Center + new GridVector2(0, this.Radius / 2.0f));
                ParentStructureLabelView._Color = locationObj.Parent.Parent.Type.Color.ToXNAColor(0.75f);
            }
            else
            {
                ParentStructureLabelView = null;
            }
        }

        public bool IsLabelVisible(VikingXNA.Scene scene)
        {
            return StructureIDLabelView.IsVisible(scene);
        }

        /// <summary>
        /// Draw the text for the location at the specified screen coordinates
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="ScreenDrawPosition">Center of the annotation in screen space, which is the coordinate system used for text</param>
        /// <param name="MagnificationFactor"></param>
        /// <param name="DirectionToVisiblePlane">The Z distance of the location to the plane viewed by user.</param>
        public void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");

            //Scale the label alpha based on the zoom factor 

            double DesiredRowsOfText = 4.0;
            double DefaultFontSize = (this.Radius * 2.0) / DesiredRowsOfText;
            StructureIDLabelView.FontSize = DefaultFontSize; //We only desire one line of text
            StructureLabelView.FontSize = DefaultFontSize / 3.0f;

            //StructureIDLabelView.Position = modelObj.VolumePosition - new GridVector2(0.0, this.Radius / 3.0f);

            StructureIDLabelView.Draw(spriteBatch, font, scene);
            StructureLabelView.Draw(spriteBatch, font, scene);
            if (ParentStructureLabelView != null)
            {
                ParentStructureLabelView.FontSize = StructureIDLabelView.FontSize / 4.0;
                ParentStructureLabelView.Draw(spriteBatch, font, scene);
            }
             
            return;
        }
    }
}
