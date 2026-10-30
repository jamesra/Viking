using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotationModel;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    /// <summary>A location view that draws arrow markers for its links to overlapping locations on adjacent sections.</summary>
    internal interface IOverlappedLinkHost
    {
        /// <summary>The markers, or null when the location has no overlapped links.</summary>
        OverlappedLinkCircleView? OverlappedLinkView { get; }
    }

    /// <summary>
    /// Draw overlapped links as a set of circles inscribed in a larger circle (circle annotations), or at the marker slots
    /// a polygon's <see cref="LabelPlacement"/> chose (run or circle)
    /// </summary>
    internal class OverlappedLinkCircleView : ICanvasGeometryView, ICanvasViewContainer, IColorView, ILabelView
    {
        private ICollection<OverlappedLocationLinkView> linkViews = [];

        private SortedSet<long> _OverlappedLinks = [];

        public ICollection<long> OverlappedLinks
        {
            get => _OverlappedLinks;
            set
            {
                _OverlappedLinks.Clear();
                _OverlappedLinks = [.. value];
                CreateViews();
            }
        }

        private readonly Circle OuterCircle_MosaicSpace;
        private readonly Circle OuterCircle_VolumeSpace;
        private readonly long ID;
        private readonly int Z;

        /// <summary>Marker positions from a polygon's label placement; null when markers go on the arcs of the outer circle.</summary>
        private readonly IReadOnlyList<MarkerSlot>? Slots;

        /// <summary>Text axis (the run's, or +X in the circle) that slots and linked locations are both ordered along.</summary>
        private readonly Geometry.Vector2 SlotAxis;

        public OverlappedLinkCircleView(Circle outerCircle_MosaicSpace, long LocationID, int ZCut, ICollection<long> OverlappedLinks)
        {
            OuterCircle_MosaicSpace = outerCircle_MosaicSpace;
            OuterCircle_VolumeSpace = outerCircle_MosaicSpace;
            BoundingBox = OuterCircle_VolumeSpace.BoundingBox;
            Z = ZCut;
            ID = LocationID;
            this.OverlappedLinks = OverlappedLinks;
        }

        /// <summary>
        /// Markers at the slots of a polygon's <paramref name="placement"/>, run or circle. Links to sections above take
        /// the up slots and links below the down slots, matched in order along the text axis so each marker sits on its
        /// link's side. The slots are fixed for a placement, so a relayout that keeps it keeps every marker in place.
        /// </summary>
        public OverlappedLinkCircleView(LabelPlacement placement, long LocationID, int ZCut, ICollection<long> OverlappedLinks)
        {
            Slots = placement.Markers;
            SlotAxis = placement.Axis;
            BoundingBox = Slots.Count == 0
                ? (placement.Kind == LabelPlacementKind.Run ? placement.Region.ToPolygon().BoundingBox : placement.Circle.BoundingBox)
                : Slots.Select(s => s.Circle.BoundingBox).Aggregate((a, b) => Geometry.Rectangle.Union(a, b));
            Z = ZCut;
            ID = LocationID;
            this.OverlappedLinks = OverlappedLinks;
        }

        public Geometry.Rectangle BoundingBox { get; }

        private Color _Color;

        public Color Color
        {
            get => _Color;

            set
            {
                _Color = value;
                foreach (IColorView view in linkViews.Cast<IColorView>())
                {
                    view.Color = value;
                }
            }
        }

        public float Alpha
        {
            get => _Color.GetAlpha();

            set
            {
                _Color.SetAlpha(value);
                foreach (IColorView view in linkViews.Cast<IColorView>())
                {
                    view.Alpha = value;
                }
            }
        }

        /// <summary>Links Del hid in the active viewer, or null when there is none (tests).</summary>
        private static TemporarilyHiddenAnnotations? Hidden => AnnotationOverlay.CurrentOverlay?.HiddenAnnotations;

        /// <summary>
        /// The markers that can be hit: a marker whose link Del hid is not there for the mouse, and none are while a pen
        /// command hides location links (<see cref="PenLocationLinks.Hidden"/>), so the stroke log and the pen cut see the
        /// location instead of a marker inside it.
        /// </summary>
        private IEnumerable<OverlappedLocationLinkView> PickableLinkViews
        {
            get
            {
                if (PenLocationLinks.Hidden)
                    return [];

                TemporarilyHiddenAnnotations? hidden = Hidden;
                return hidden is null || hidden.IsEmpty ? linkViews : linkViews.Where(l => !hidden.IsHidden(l));
            }
        }

        public double Distance(Geometry.Vector2 Position) => PickableLinkViews.Select(c => c.Distance(Position)).DefaultIfEmpty(double.MaxValue).Min();

        public double Distance(Microsoft.SqlServer.Types.SqlGeometry shape) => PickableLinkViews.Select(c => c.Distance(shape)).DefaultIfEmpty(double.MaxValue).Min();

        public double DistanceFromCenterNormalized(Geometry.Vector2 Position) => PickableLinkViews.Select(c => c.DistanceFromCenterNormalized(Position)).DefaultIfEmpty(double.MaxValue).Min();

        public bool Contains(Geometry.Vector2 Position) => PickableLinkViews.Any(c => c.Contains(Position));

        public bool Intersects(LineSegment line) => PickableLinkViews.Any(c => c.Intersects(line));

        public bool IsVisible(Scene scene) => linkViews.Any(c => c.IsVisible(scene));

        public bool IsLabelVisible(Scene scene) => linkViews.Any(l => l.IsLabelVisible(scene));

        public ICanvasView GetAnnotationAtPosition(Geometry.Vector2 position) => PickableLinkViews.FirstOrDefault(l => l.Contains(position));

        /// <summary>Every marker under <paramref name="position"/>, hidden or not, so Del and Insert can pick one by its link.</summary>
        public IEnumerable<OverlappedLocationLinkView> LinkViewsAt(Geometry.Vector2 position) => linkViews.Where(l => l.Contains(position));

        public int VisualHeight => 0;

        private void CreateViews()
        {
            ICollection<LocationObj> links = Store.Locations.GetObjectsByIDs(_OverlappedLinks, false);
            linkViews = Slots is null
                ? CalculateOverlappedLocationCircles(OuterCircle_VolumeSpace, ID, links, Z)
                : PlaceOverlappedLocationCircles(Slots, SlotAxis, ID, links, Z);
        }

        /// <summary>
        /// Puts each linked location in a slot of its direction. Both are sorted by position along <paramref name="axis"/>,
        /// so the leftmost link takes the leftmost slot. Links on the same section have no slot, as on the circle's arcs.
        /// </summary>
        private static ICollection<OverlappedLocationLinkView> PlaceOverlappedLocationCircles(IReadOnlyList<MarkerSlot> slots, Geometry.Vector2 axis, long locationID, ICollection<LocationObj> OverlappingLinks, int ZCut)
        {
            List<OverlappedLocationLinkView> listCircles = new(OverlappingLinks.Count);
            foreach (bool up in new[] { true, false })
            {
                LocationObj[] links = [.. OverlappingLinks.Where(loc => up ? loc.Z > ZCut : loc.Z < ZCut)
                                                          .OrderBy(loc => Geometry.Vector2.Dot(loc.VolumeCenter(), axis))];
                MarkerSlot[] directionSlots = [.. slots.Where(s => s.IsUp == up).OrderBy(s => Geometry.Vector2.Dot(s.Center, axis))];
                for (int i = 0; i < Math.Min(links.Length, directionSlots.Length); i++)
                {
                    listCircles.Add(new OverlappedLocationLinkView(locationID, links[i], directionSlots[i].Circle, up, ZCut));
                }
            }

            return listCircles;
        }

        /// <summary>
        /// A linked location overlapping with our location is drawn as a small circle.  This function stores the position of those smaller circles along an arc
        /// </summary>
        /// <returns></returns>
        private static ICollection<OverlappedLocationLinkView> CalculateOverlappedLocationCircles(Circle OuterCircle, long locationID, ICollection<LocationObj> OverlappingLinks, int ZCut)
        {
            //SortedDictionary<OverlappedLocationView, LocationObj> listCircles = new SortedDictionary<OverlappedLocationView, LocationObj>();
            List<OverlappedLocationLinkView> listCircles = new(OverlappingLinks.Count);

            List<LocationObj> listLinksAbove = [.. OverlappingLinks.Where(loc => loc.Z > ZCut)];
            List<LocationObj> listLinksBelow = [.. OverlappingLinks.Where(loc => loc.Z < ZCut)];
            //List<LocationObj> listLinksEqual = OverlappingLinks.Where(loc => loc.Z == ZCut).ToList();

            listLinksAbove = [.. listLinksAbove.OrderBy(L => -L.VolumeCenter().X).ThenBy(L => L.VolumeCenter().Y)];
            listLinksBelow = [.. listLinksBelow.OrderBy(L => L.VolumeCenter().X).ThenBy(L => L.VolumeCenter().Y)];

            //Figure out how large link images would be
            double linkRadius = OuterCircle.Radius / 6;

            double linkArcNormalizedDistanceFromCenter = 0.75;
            double linkArcDistanceFromCenter = linkArcNormalizedDistanceFromCenter * OuterCircle.Radius;
            double circumferenceOfLinkArc = linkArcDistanceFromCenter * Math.PI; //Don't multiply by two since we only use top half of circle

            double UpperArcLinkRadius = linkRadius;
            double LowerArcLinkRadius = linkRadius;
            //double EqualArcLinkRadius = linkRadius;

            //See if we will run out of room for links
            if (UpperArcLinkRadius * listLinksAbove.Count > circumferenceOfLinkArc)
            {
                UpperArcLinkRadius = circumferenceOfLinkArc / listLinksAbove.Count;
            }

            if (LowerArcLinkRadius * listLinksBelow.Count > circumferenceOfLinkArc)
            {
                LowerArcLinkRadius = circumferenceOfLinkArc / listLinksBelow.Count;
            }
            /*
            if (EqualArcLinkRadius * listLinksBelow.Count > OuterCircle.Radius * 2)
            {
                EqualArcLinkRadius = OuterCircle.Radius * 2 / listLinksBelow.Count;
            }
            */
            double UpperArcStepSize = UpperArcLinkRadius / (circumferenceOfLinkArc / 2);
            double LowerArcStepSize = LowerArcLinkRadius / (circumferenceOfLinkArc / 2);

            double halfNumLinksAbove = listLinksAbove.Count / 2;
            double angleOffset = ((double)(1 - listLinksAbove.Count) % 2) * (UpperArcStepSize / 2);
            for (int iLocAbove = 0; iLocAbove < listLinksAbove.Count; iLocAbove++)
            {
                LocationObj linkLoc = listLinksAbove[iLocAbove];

                //Figure out where the link should be drawn. 
                //Allocate the top 180 degree arc for links above, the bottom 180 for links below

                double angle = ((((iLocAbove - halfNumLinksAbove) * UpperArcStepSize) - angleOffset) * Math.PI); //- angleOffset;

                Vector3 positionOffset = new((float)Math.Sin(angle), (float)Math.Cos(angle), 0);
                positionOffset *= (float)linkArcDistanceFromCenter;

                Circle circle = new(OuterCircle.Center + new Geometry.Vector2(positionOffset.X, positionOffset.Y), UpperArcLinkRadius);

                OverlappedLocationLinkView overlapLocation = new(locationID, linkLoc, circle, true, ZCut);
                listCircles.Add(overlapLocation);
            }

            double halfNumLinksBelow = listLinksBelow.Count / 2;
            angleOffset = ((double)(1 - listLinksBelow.Count) % 2) * (LowerArcStepSize / 2);
            for (int iLocBelow = 0; iLocBelow < listLinksBelow.Count; iLocBelow++)
            {
                LocationObj linkLoc = listLinksBelow[iLocBelow];

                //Figure out where the link should be drawn. 
                //Allocate the top 180 degree arc for links above, the bottom 180 for links below

                double angle = ((((iLocBelow - halfNumLinksBelow) * LowerArcStepSize) - angleOffset) * Math.PI) + Math.PI;

                Vector3 positionOffset = new((float)Math.Sin(angle), (float)Math.Cos(angle), 0);
                positionOffset *= (float)linkArcDistanceFromCenter;

                Circle circle = new(OuterCircle.Center + new Geometry.Vector2(positionOffset.X, positionOffset.Y), LowerArcLinkRadius);

                OverlappedLocationLinkView overlapLocation = new(locationID, linkLoc, circle, false, ZCut);
                listCircles.Add(overlapLocation);
            }
            /* This is code to create circles for error links on the same section
            double centerStepSize = EqualArcLinkRadius * 2;
            double firstCenterOffset = -centerStepSize * ((listLinksEqual.Count - 1) / 2);
            for (int iLocEqual = 0; iLocEqual < listLinksEqual.Count; iLocEqual++)
            {
                LocationObj linkLoc = listLinksBelow[iLocEqual];

                Vector3 position = new Vector3((float)(firstCenterOffset + (iLocEqual * centerStepSize)), 0, 0);
                Circle circle = new Circle(position, EqualArcLinkRadius);
                OverlappedLocationLinkView overlapLocation = new OverlappedLocationLinkView(locationID, linkLoc, circle, false);
                listCircles.Add(overlapLocation);
            }
            */
            return listCircles;
        }

        /// <summary>
        /// Draws the markers of <paramref name="listToDraw"/>, skipping those Del hid and all of them while a pen command
        /// hides location links (<see cref="PenLocationLinks.Hidden"/>). Called by the circle, polygon and closed curve views.
        /// </summary>
        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          OverlappedLinkCircleView[] listToDraw)
        {
            if (PenLocationLinks.Hidden)
                return;

            OverlappedLocationLinkView[] linkViewArray = [.. listToDraw.SelectMany(l => l.linkViews)];
            TemporarilyHiddenAnnotations? hidden = Hidden;
            if (hidden is null)
            {
                OverlappedLocationLinkView.Draw(device, scene, basicEffect, overlayEffect, linkViewArray);
                return;
            }

            // A marker Del hid is skipped, and fades back in like a link line
            List<OverlappedLocationLinkView> visible = hidden.VisibleToDraw(linkViewArray);
            using (hidden.ApplyFade(visible))
            {
                OverlappedLocationLinkView.Draw(device, scene, basicEffect, overlayEffect, [.. visible]);
            }
        }

        /// <summary>Labels the markers drawn this frame: none while a pen command hides location links.</summary>
        public void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene)
        {
            if (PenLocationLinks.Hidden)
                return;

            TemporarilyHiddenAnnotations? hidden = Hidden;
            foreach (OverlappedLocationLinkView ov in linkViews.Where(ov => ov.IsLabelVisible(scene)))
            {
                float fade = hidden?.FadeIn(ov) ?? 1f;
                if (fade > 0f)
                    ov.DrawLabel(spriteBatch, font, scene, fade);
            }
        }
    }
}
