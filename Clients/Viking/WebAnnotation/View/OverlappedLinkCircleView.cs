using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.View
{
    /// <summary>
    /// Draw overlapped links as a set of circles inscribed in a larger circle
    /// </summary>
    class OverlappedLinkCircleView : ICanvasGeometryView, ICanvasViewContainer, IColorView, ILabelView
    {
        ICollection<OverlappedLocationLinkView> linkViews = new List<OverlappedLocationLinkView>();

        private SortedSet<long> _OverlappedLinks = new SortedSet<long>();

        public ICollection<long> OverlappedLinks
        {
            get
            {
                return _OverlappedLinks;
            }
            set
            {
                _OverlappedLinks.Clear();
                _OverlappedLinks = new SortedSet<long>(value);
                CreateViews();
            }
        }

        GridCircle OuterCircle_MosaicSpace;
        GridCircle OuterCircle_VolumeSpace;

        long ID;
        int Z;

        public OverlappedLinkCircleView(GridCircle outerCircle_MosaicSpace, long LocationID, int ZCut, ICollection<long> OverlappedLinks)
        {
            this.OuterCircle_MosaicSpace = outerCircle_MosaicSpace;
            this.OuterCircle_VolumeSpace = outerCircle_MosaicSpace;
            this.Z = ZCut;
            this.ID = LocationID;
            this.OverlappedLinks = OverlappedLinks;
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return OuterCircle_VolumeSpace.BoundingBox;
            }
        }

        private Color _Color;

        public Color Color
        {
            get
            {
                return _Color;
            }

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
            get
            {
                return _Color.GetAlpha();
            }

            set
            {
                _Color.SetAlpha(value);
                foreach (IColorView view in linkViews.Cast<IColorView>())
                {
                    view.Alpha = value;
                }
            }
        }

        public double Distance(GridVector2 Position)
        {
            return linkViews.Min(c => c.Distance(Position));
        }

        public double Distance(Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            return linkViews.Min(c => c.Distance(shape));
        }

        public double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return linkViews.Min(c => c.DistanceFromCenterNormalized(Position));
        }

        public bool Contains(GridVector2 Position)
        {
            return linkViews.Any(c => c.Contains(Position));
        }

        public bool Intersects(GridLineSegment line)
        {
            return linkViews.Any(c => c.Intersects(line));
        }

        public bool IsVisible(Scene scene)
        {
            return linkViews.Any(c => c.IsVisible(scene));
        }

        public bool IsLabelVisible(Scene scene)
        {
            return linkViews.Any(l => l.IsLabelVisible(scene));
        }

        public ICanvasView GetAnnotationAtPosition(GridVector2 position)
        {
            ICanvasView annotation = linkViews.FirstOrDefault(l => l.Contains(position) == true) as ICanvasGeometryView;
            if (annotation == null)
                return null;

            return annotation;
        }

        public int VisualHeight
        {
            get
            {
                return 0;
            }
        }

        private void CreateViews()
        {
            this.linkViews = CalculateOverlappedLocationCircles(this.OuterCircle_VolumeSpace, ID, Store.Locations.GetObjectsByIDs(_OverlappedLinks, false), this.Z);
        }

        /// <summary>
        /// A linked location overlapping with our location is drawn as a small circle.  This function stores the position of those smaller circles along an arc
        /// </summary>
        /// <returns></returns>
        private static ICollection<OverlappedLocationLinkView> CalculateOverlappedLocationCircles(GridCircle OuterCircle, long locationID, ICollection<LocationObj> OverlappingLinks, int ZCut)
        {
            //SortedDictionary<OverlappedLocationView, LocationObj> listCircles = new SortedDictionary<OverlappedLocationView, LocationObj>();
            List<OverlappedLocationLinkView> listCircles = new List<OverlappedLocationLinkView>(OverlappingLinks.Count);

            List<LocationObj> listLinksAbove = OverlappingLinks.Where(loc => loc.Z > ZCut).ToList();
            List<LocationObj> listLinksBelow = OverlappingLinks.Where(loc => loc.Z < ZCut).ToList();

            listLinksAbove = listLinksAbove.OrderBy(L => -L.VolumePosition.X).ThenBy(L => L.VolumePosition.Y).ToList();
            listLinksBelow = listLinksBelow.OrderBy(L => L.VolumePosition.X).ThenBy(L => L.VolumePosition.Y).ToList();

            //Figure out how large link images would be
            double linkRadius = OuterCircle.Radius / 6;

            double linkArcNormalizedDistanceFromCenter = 0.75;
            double linkArcDistanceFromCenter = linkArcNormalizedDistanceFromCenter * OuterCircle.Radius;
            double circumferenceOfLinkArc = linkArcDistanceFromCenter * Math.PI; //Don't multiply by two since we only use top half of circle

            double UpperArcLinkRadius = linkRadius;
            double LowerArcLinkRadius = linkRadius;

            //See if we will run out of room for links
            if (linkRadius * listLinksAbove.Count > circumferenceOfLinkArc)
            {
                UpperArcLinkRadius = circumferenceOfLinkArc / listLinksAbove.Count;
            }

            if (linkRadius * listLinksBelow.Count > circumferenceOfLinkArc)
            {
                LowerArcLinkRadius = circumferenceOfLinkArc / listLinksBelow.Count;
            }

            double UpperArcStepSize = UpperArcLinkRadius / (circumferenceOfLinkArc / 2);
            double LowerArcStepSize = LowerArcLinkRadius / (circumferenceOfLinkArc / 2);

            double halfNumLinksAbove = listLinksAbove.Count / 2;
            double angleOffset = ((double)(1 - listLinksAbove.Count) % 2) * (UpperArcStepSize / 2);
            for (int iLocAbove = 0; iLocAbove < listLinksAbove.Count; iLocAbove++)
            {
                LocationObj linkLoc = listLinksAbove[iLocAbove];

                //Figure out where the link should be drawn. 
                //Allocate the top 180 degree arc for links above, the bottom 180 for links below

                double angle = (((((double)iLocAbove - halfNumLinksAbove) * UpperArcStepSize) - angleOffset) * Math.PI); //- angleOffset;

                Vector3 positionOffset = new Vector3((float)Math.Sin(angle), (float)Math.Cos(angle), (float)0);
                positionOffset *= (float)linkArcDistanceFromCenter;

                GridCircle circle = new GridCircle(OuterCircle.Center + new GridVector2(positionOffset.X, positionOffset.Y), UpperArcLinkRadius);

                OverlappedLocationLinkView overlapLocation = new OverlappedLocationLinkView(locationID, linkLoc, circle, true);
                listCircles.Add(overlapLocation);
            }

            double halfNumLinksBelow = listLinksBelow.Count / 2;
            angleOffset = ((double)(1 - listLinksBelow.Count) % 2) * (LowerArcStepSize / 2);
            for (int iLocBelow = 0; iLocBelow < listLinksBelow.Count; iLocBelow++)
            {
                LocationObj linkLoc = listLinksBelow[iLocBelow];

                //Figure out where the link should be drawn. 
                //Allocate the top 180 degree arc for links above, the bottom 180 for links below

                double angle = (((((double)iLocBelow - halfNumLinksBelow) * LowerArcStepSize) - angleOffset) * Math.PI) + Math.PI;

                Vector3 positionOffset = new Vector3((float)Math.Sin(angle), (float)Math.Cos(angle), (float)0);
                positionOffset *= (float)linkArcDistanceFromCenter;

                GridCircle circle = new GridCircle(OuterCircle.Center + new GridVector2(positionOffset.X, positionOffset.Y), LowerArcLinkRadius);

                OverlappedLocationLinkView overlapLocation = new OverlappedLocationLinkView(locationID, linkLoc, circle, false);
                listCircles.Add(overlapLocation);
            }

            return listCircles;
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          OverlappedLinkCircleView[] listToDraw)
        {
            OverlappedLocationLinkView[] linkViewArray = listToDraw.SelectMany(l => l.linkViews).ToArray();
            OverlappedLocationLinkView.Draw(device, scene, basicEffect, overlayEffect, linkViewArray);
        }

        public void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene)
        {

            foreach (ILabelView ov in this.linkViews.Cast<ILabelView>().Where(ov => ov.IsLabelVisible(scene)))
            {
                ov.DrawLabel(spriteBatch, font, scene);
            }

            return;
        }

    }
}
