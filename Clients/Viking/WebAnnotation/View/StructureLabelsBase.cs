using Geometry;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using VikingXNAGraphics;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    /// <summary>
    /// Text and drawing shared by the labels of a structure's location: the type code and ID, the structure's label, its
    /// tags and its parent's ID, each a <see cref="LabelView"/> sized from <see cref="DefaultFontSize"/>. Subclasses
    /// decide where the rows go.
    /// </summary>
    /// <remarks>
    /// A new set built to replace an old one (a polygon label moving to a new placement) can ease its rows there from
    /// where the old set showed them; see <see cref="TransitionFrom"/>.
    /// </remarks>
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

        /// <summary>
        /// Each row's view and where it was built to be, ID row first, in drawing order. Set by
        /// <see cref="RecordTargets"/>; the views' own Position, Rotation, FontSize and MaxLineWidth are rewritten on every
        /// draw, so this, not the views, is what another set reads from a background thread.
        /// </summary>
        private RowTarget[] targets = [];

        /// <summary>Where each row was shown when the transition started; null when this set did not replace a moved one.</summary>
        private Dictionary<LabelRowKind, LabelPose>? transitionFrom;

        /// <summary><see cref="Stopwatch"/> timestamp the transition started at.</summary>
        private long transitionStart;

        /// <summary>1 while a label drawn since the last <see cref="TakeAnimatingFrame"/> was still easing.</summary>
        private static int animatingFrame;

        /// <summary>One row's view, its font scale relative to the ID row, and its built centre, angle and line width.</summary>
        private readonly struct RowTarget
        {
            public readonly LabelRowKind Kind;
            public readonly LabelView View;
            public readonly double Scale;
            public readonly Geometry.Vector2 Center;
            public readonly double Angle;
            public readonly double MaxLineWidth;

            public RowTarget(LabelRowKind kind, LabelView view, double scale)
            {
                Kind = kind;
                View = view;
                Scale = scale;
                Center = view.Position;
                Angle = view.Rotation;
                MaxLineWidth = view.MaxLineWidth;
            }
        }

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

        /// <summary>
        /// Records where the row views were just built to be. Subclasses call it whenever they (re)create the views and
        /// before the set is published to the drawing thread.
        /// </summary>
        protected void RecordTargets()
        {
            List<RowTarget> rows = new(4);
            if (StructureIDLabelView != null)
                rows.Add(new RowTarget(LabelRowKind.ID, StructureIDLabelView, 1.0));

            if (ShowAttributeLabels && StructureLabelView != null)
                rows.Add(new RowTarget(LabelRowKind.StructureLabel, StructureLabelView, StructureLabelScale));

            if (ShowAttributeLabels && StructureAttributeView != null)
                rows.Add(new RowTarget(LabelRowKind.Tags, StructureAttributeView, TagScale));

            if (ParentStructureLabelView != null)
                rows.Add(new RowTarget(LabelRowKind.Parent, ParentStructureLabelView, ParentScale));

            targets = [.. rows];
        }

        /// <summary>
        /// Eases this set's rows in from where <paramref name="previous"/>, the set it replaces, shows them now, over
        /// <see cref="LabelEasing.DurationSeconds"/>: each row present in both moves, turns the short way and rescales
        /// from its old pose to its new one, so a circle label and a run label animate between each other row by row.
        /// A row only this set has appears at once. When the two sets were built to the same poses (the label kept its
        /// placement) the previous set's transition carries on unchanged, so a relayout mid-animation neither restarts
        /// nor cuts it short. Call before the set is published; safe on a background thread while
        /// <paramref name="previous"/> is being drawn.
        /// </summary>
        public void TransitionFrom(StructureLabelsBase? previous)
        {
            if (previous is null)
                return;

            if (SameTargets(previous))
            {
                transitionFrom = previous.transitionFrom;
                transitionStart = previous.transitionStart;
                return;
            }

            long now = Stopwatch.GetTimestamp();
            Dictionary<LabelRowKind, LabelPose> from = new();
            bool moves = false;
            foreach (RowTarget row in targets)
            {
                if (!previous.TryShownPose(row.Kind, now, out LabelPose shown))
                    continue;

                from[row.Kind] = shown;
                LabelPose target = TargetPose(row);
                moves |= shown.Center != target.Center || shown.Angle != target.Angle || shown.FontSize != target.FontSize;
            }

            if (!moves)
                return;

            transitionFrom = from;
            transitionStart = now;
        }

        /// <summary>True when both sets put the same rows at the same centres, angles and font size.</summary>
        private bool SameTargets(StructureLabelsBase other)
        {
            if (other.targets.Length != targets.Length || other.DefaultFontSize != DefaultFontSize)
                return false;

            for (int i = 0; i < targets.Length; i++)
            {
                RowTarget a = targets[i], b = other.targets[i];
                if (a.Kind != b.Kind || a.Center != b.Center || a.Angle != b.Angle)
                    return false;
            }

            return true;
        }

        private LabelPose TargetPose(RowTarget row) => new(row.Center, row.Angle, DefaultFontSize * row.Scale);

        /// <summary>Eased progress of the transition at <paramref name="now"/>; 1 when there is none or it has finished.</summary>
        private double TransitionProgress(long now) =>
            transitionFrom is null ? 1 : LabelEasing.Progress((double)(now - transitionStart) / Stopwatch.Frequency);

        /// <summary>Where the <paramref name="kind"/> row is shown at <paramref name="now"/>, mid-transition or at rest.</summary>
        private bool TryShownPose(LabelRowKind kind, long now, out LabelPose pose)
        {
            foreach (RowTarget row in targets)
            {
                if (row.Kind == kind)
                {
                    pose = ShownPose(row, TransitionProgress(now));
                    return true;
                }
            }

            pose = default;
            return false;
        }

        private LabelPose ShownPose(RowTarget row, double progress)
        {
            LabelPose target = TargetPose(row);
            Dictionary<LabelRowKind, LabelPose>? from = transitionFrom;
            if (progress >= 1 || from is null || !from.TryGetValue(row.Kind, out LabelPose start))
                return target;

            return LabelEasing.Interpolate(start, target, progress);
        }

        /// <summary>
        /// True when a label drawn since the last call was still easing to a new placement, so the viewer should draw
        /// another frame; clears the flag. The overlay calls it once per frame after drawing labels, and so stays idle
        /// once every label has landed.
        /// </summary>
        internal static bool TakeAnimatingFrame() => Interlocked.Exchange(ref animatingFrame, 0) == 1;

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

            double progress = TransitionProgress(Stopwatch.GetTimestamp());
            if (progress < 1)
                Interlocked.Exchange(ref animatingFrame, 1);

            foreach (RowTarget row in targets)
            {
                LabelPose pose = ShownPose(row, progress);
                double targetFont = DefaultFontSize * row.Scale;

                // Line widths scale with the font so a row wraps the same way throughout the transition
                double widthScale = targetFont > 0 ? pose.FontSize / targetFont : 1.0;
                double lineWidth = (row.Kind == LabelRowKind.ID ? IDLineWidth : row.MaxLineWidth) * widthScale;
                double fontSize = pose.FontSize;
                if (row.Kind == LabelRowKind.ID && OscillateSize)
                {
                    double Scalar = GetOscillationFactor;
                    fontSize *= Scalar;
                    lineWidth *= Scalar > 1 ? Scalar : 1.0;
                }

                LabelView view = row.View;
                view.Position = pose.Center;
                view.Rotation = (float)pose.Angle;
                view.FontSize = fontSize;
                view.MaxLineWidth = lineWidth;
                view.Draw(spriteBatch, font, scene);
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
