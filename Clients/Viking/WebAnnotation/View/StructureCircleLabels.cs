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
        public LabelView StructureAttributeView;
        public LabelView ParentStructureLabelView;

        LocationObj locationObj = null;

        GridCircle VolumeCircle;

        readonly bool ShowAttributeLabels = true;

        public double DesiredRowsOfText { get; set; } = 4.0;

        public double DefaultFontSize
        {
            get
            {
                return (this.Radius * 2.0) / DesiredRowsOfText;
            }
        }

        public double Radius
        {
            get { return VolumeCircle.Radius; }
            set
            {
                VolumeCircle.Radius = value;
                CreateLabelObjects();
            }
        }

        public StructureCircleLabels(LocationObj obj, GridCircle circle, bool ShowAttributeLabels = true)
        {
            VolumeCircle = circle;
            locationObj = obj;
            this.ShowAttributeLabels = ShowAttributeLabels;
            CreateLabelObjects();
        }

        protected string StructureIDLabelWithTypeCode(StructureObj obj)
        {
            if (obj == null)
                return "";

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
            if (locationObj.Parent == null)
                return "";

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
            if (locationObj.Parent == null)
                return InfoLabel;

            if (locationObj.Parent.Label != null)
                InfoLabel = locationObj.Parent.Label.Trim();

            return InfoLabel;
        }

        private void CreateLabelObjects()
        {
            {
                double Height = this.VolumeCircle.Radius / 3.0f;
                StructureIDLabelView = new LabelView(StructureIDLabelWithTypeCode(this.locationObj.Parent), this.VolumeCircle.Center - new GridVector2(0, Height));
                StructureIDLabelView.MaxLineWidth = GridCircle.WidthAtHeight(Height / this.Radius) * (this.Radius * 2.0);
                StructureIDLabelView._Color = this.locationObj.IsUnverifiedTerminal ? Color.Yellow : Color.Black;
            }

            if (ShowAttributeLabels)
            {
                string Label = this.StructureLabel();
                if (Label == null || Label?.Length == 0)
                {
                    StructureLabelView = null;
                }
                else
                {
                    double height = this.Radius / 2.0f;
                    StructureLabelView = new LabelView(Label, this.VolumeCircle.Center + new GridVector2(0, height));
                    StructureLabelView.MaxLineWidth = GridCircle.WidthAtHeight(height / this.Radius) * (this.Radius * 2.0);
                }

                string Tags = this.TagLabel();
                if (Tags == null || Tags?.Length == 0)
                {
                    StructureAttributeView = null;
                }
                else
                {
                    double height = this.Radius / 4.0f;
                    StructureAttributeView = new LabelView(Tags, this.VolumeCircle.Center + new GridVector2(0, height));
                    StructureAttributeView.MaxLineWidth = GridCircle.WidthAtHeight(height / this.Radius) * (this.Radius * 2.0);
                }
            }

            if (locationObj.Parent != null && locationObj.Parent.ParentID.HasValue)
            {
                double height = this.Radius / 2.0f;
                ParentStructureLabelView = new LabelView(locationObj.Parent.ParentID.ToString(), this.VolumeCircle.Center + new GridVector2(0, height));
                ParentStructureLabelView._Color = Color.Red; //locationObj.Parent.Parent.Type.Color.ToXNAColor(0.75f);
                ParentStructureLabelView.MaxLineWidth = GridCircle.WidthAtHeight(height / this.Radius) * (this.Radius * 2.0);
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
            bool OscillateSize = this.locationObj.IsLastEditedAnnotation();

            StructureIDLabelView.FontSize = DefaultFontSize; //We only desire one line of text

            if(OscillateSize)
            {
                double Scalar = GetOscillationFactor;
                StructureIDLabelView.FontSize *= Scalar;
                StructureIDLabelView.MaxLineWidth *= Scalar > 1 ? Scalar : 1.0;
            }
            else
            {
                StructureIDLabelView.MaxLineWidth = this.Radius * 2.0;
            }

            StructureIDLabelView.Draw(spriteBatch, font, scene);

            if (ShowAttributeLabels)
            {
                if (StructureLabelView != null)
                {
                    StructureLabelView.FontSize = DefaultFontSize / 2.0f;

                    //StructureIDLabelView.Position = modelObj.VolumePosition - new GridVector2(0.0, this.Radius / 3.0f);

                    StructureLabelView.Draw(spriteBatch, font, scene);
                }

                if(StructureAttributeView != null)
                {
                    StructureAttributeView.FontSize = DefaultFontSize / 4.0f;
                    StructureAttributeView.Draw(spriteBatch, font, scene);
                }
            }

            if (ParentStructureLabelView != null)
            {
                ParentStructureLabelView.FontSize = DefaultFontSize / 2.0f;
                ParentStructureLabelView.Draw(spriteBatch, font, scene);
            }

            return;
        }

        /// <summary>
        /// Returns a number from .9 to 1.1 on a 1Hz wave
        /// </summary>
        private static double GetOscillationFactor 
        {
            get
            {
                double SecondsPerCycle = 3;
                double Hz = 1.0 / SecondsPerCycle;
                double ms = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
                ms %= SecondsPerCycle;
                ms /= SecondsPerCycle; 
                ms *= Math.PI * 2; 
                return (Math.Sin(ms) / 20.0) + 1.0; 
            }
    }
    }
}
