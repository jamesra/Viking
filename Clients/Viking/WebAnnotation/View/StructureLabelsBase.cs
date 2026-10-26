using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.View
{
    /// <summary>
    /// Text and drawing shared by the labels of a structure's location: the type code and ID, the structure's label, its
    /// tags and its parent's ID, each a <see cref="LabelView"/> sized from <see cref="DefaultFontSize"/>. Subclasses
    /// decide where the rows go.
    /// </summary>
    public abstract class StructureLabelsBase : ILabelView
    {
        /// <summary>Font size of the structure label row relative to the ID row.</summary>
        public const double StructureLabelScale = 0.5;

        /// <summary>Font size of the tag row relative to the ID row.</summary>
        public const double TagScale = 0.25;

        /// <summary>Font size of the parent ID row relative to the ID row.</summary>
        public const double ParentScale = 0.5;

        public LabelView StructureIDLabelView;
        public LabelView StructureLabelView;
        public LabelView StructureAttributeView;
        public LabelView ParentStructureLabelView;
        protected readonly LocationObj locationObj;
        protected readonly bool ShowAttributeLabels;

        protected StructureLabelsBase(LocationObj obj, bool showAttributeLabels)
        {
            locationObj = obj;
            ShowAttributeLabels = showAttributeLabels;
        }

        /// <summary>Font size of the ID row.</summary>
        public abstract double DefaultFontSize { get; }

        /// <summary>Widest the ID row may be before it wraps, when it is not oscillating.</summary>
        protected abstract double IDLineWidth { get; }

        /// <summary>Which text a label row holds.</summary>
        protected enum LabelRowKind
        {
            StructureLabel,
            Tags,
            ID,
            Parent
        }

        /// <summary>The rows a location's label shows, top to bottom, with their font size relative to the ID row.</summary>
        protected static IReadOnlyList<(LabelRowKind Kind, string Text, double Scale)> Rows(LocationObj obj, bool showAttributeLabels)
        {
            List<(LabelRowKind, string, double)> rows = new(4);
            if (showAttributeLabels)
            {
                string label = StructureLabel(obj);
                if (!string.IsNullOrEmpty(label))
                    rows.Add((LabelRowKind.StructureLabel, label, StructureLabelScale));

                string tags = TagLabel(obj);
                if (!string.IsNullOrEmpty(tags))
                    rows.Add((LabelRowKind.Tags, tags, TagScale));
            }

            rows.Add((LabelRowKind.ID, StructureIDLabelWithTypeCode(obj.Parent), 1.0));

            if (obj.Parent != null && obj.Parent.ParentID.HasValue)
                rows.Add((LabelRowKind.Parent, obj.Parent.ParentID.ToString(), ParentScale));

            return rows;
        }

        protected static string StructureIDLabelWithTypeCode(StructureObj obj)
        {
            if (obj is null)
            {
                return "";
            }

            return obj.Type.Code + " " + obj.ID.ToString();
        }

        protected static string TagLabel(LocationObj obj)
        {
            if (obj.Parent is null)
            {
                return "";
            }

            string InfoLabel = "";
            foreach (ObjAttribute tag in obj.Parent.Attributes)
            {
                InfoLabel += tag.ToString() + " ";
            }

            foreach (ObjAttribute tag in obj.Attributes)
            {
                InfoLabel += tag.ToString() + " ";
            }

            return InfoLabel.Trim();
        }

        protected static string StructureLabel(LocationObj obj)
        {
            string InfoLabel = "";
            if (obj.Parent is null)
            {
                return InfoLabel;
            }

            if (obj.Parent.Label != null)
            {
                InfoLabel = obj.Parent.Label.Trim();
            }

            return InfoLabel;
        }

        /// <summary>Colour of the ID row: orange for an unverified terminal, otherwise black.</summary>
        protected Color IDColor => locationObj.IsUnverifiedTerminal ? Color.Orange : Color.Black;

        public bool IsLabelVisible(VikingXNA.Scene scene) => StructureIDLabelView.IsVisible(scene);

        /// <summary>
        /// Draw the text for the location at the specified screen coordinates
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="scene"></param>
        public void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene)
        {
            if (font is null)
            {
                throw new ArgumentNullException("font");
            }

            if (spriteBatch is null)
            {
                throw new ArgumentNullException("spriteBatch");
            }

            //Scale the label alpha based on the zoom factor
            bool OscillateSize = locationObj.IsLastEditedAnnotation();

            StructureIDLabelView.FontSize = DefaultFontSize; //We only desire one line of text

            if (OscillateSize)
            {
                double Scalar = GetOscillationFactor;
                StructureIDLabelView.FontSize *= Scalar;
                StructureIDLabelView.MaxLineWidth *= Scalar > 1 ? Scalar : 1.0;
            }
            else
            {
                StructureIDLabelView.MaxLineWidth = IDLineWidth;
            }

            StructureIDLabelView.Draw(spriteBatch, font, scene);

            if (ShowAttributeLabels)
            {
                if (StructureLabelView != null)
                {
                    StructureLabelView.FontSize = DefaultFontSize * StructureLabelScale;
                    StructureLabelView.Draw(spriteBatch, font, scene);
                }

                if (StructureAttributeView != null)
                {
                    StructureAttributeView.FontSize = DefaultFontSize * TagScale;
                    StructureAttributeView.Draw(spriteBatch, font, scene);
                }
            }

            if (ParentStructureLabelView != null)
            {
                ParentStructureLabelView.FontSize = DefaultFontSize * ParentScale;
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
