using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using VikingXNA;
using WebAnnotationModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WebAnnotation.View
{
    /// <summary>
    /// Draw overlapped links as a set of circles inscribed in a larger circle
    /// </summary>
    class OverlappedLinkCircleView : ICanvasView, ICanvasViewContainer
    {
        ICollection<OverlappedLocationView> linkViews = new List<OverlappedLocationView>();

        public ICollection<LocationObj> OverlappingLinks
        {
            protected get
            {
                if (linkViews == null)
                    return new List<LocationObj>();

                return linkViews.Select(lv => lv.modelObj).ToList();
            }
            set
            {
                this.linkViews = CalculateOverlappedLocationCircles(this.OuterCircle, value, this.Z);
            }
        }

        GridCircle OuterCircle;

        int Z;

        public OverlappedLinkCircleView(GridCircle outerCircle, int ZCut, ICollection<LocationObj> OverlappedLinks)
        {
            this.OuterCircle = outerCircle;
            this.Z = ZCut;
            this.OverlappingLinks = OverlappedLinks;
        }

        public GridRectangle BoundingBox
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double Distance(GridVector2 Position)
        {
            return linkViews.Min(c => c.Distance(Position));
        }

        public double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return linkViews.Min(c => c.DistanceFromCenterNormalized(Position));
        }

        public bool Intersects(GridVector2 Position)
        {
            return linkViews.Any(c => c.Intersects(Position));
        }

        public bool IsVisible(Scene scene)
        {
            return linkViews.Any(c => c.IsVisible(scene));
        }

        public ICanvasView GetAnnotationAtPosition(GridVector2 position, out double distancetocenternormalized)
        {
            distancetocenternormalized = double.MaxValue;

            OverlappedLocationView annotation = linkViews.Where(l => l.Intersects(position) == true).FirstOrDefault();
            if (annotation == null)
                return null;

            distancetocenternormalized = annotation.DistanceFromCenterNormalized(position);
            return annotation;
        } 

        /// <summary>
        /// A linked location overlapping with our location is drawn as a small circle.  This function stores the position of those smaller circles along an arc
        /// </summary>
        /// <returns></returns>
        private static ICollection<OverlappedLocationView> CalculateOverlappedLocationCircles(GridCircle OuterCircle, ICollection<LocationObj> OverlappingLinks, int ZCut)
        {
            //SortedDictionary<OverlappedLocationView, LocationObj> listCircles = new SortedDictionary<OverlappedLocationView, LocationObj>();
            List<OverlappedLocationView> listCircles = new List<OverlappedLocationView>(OverlappingLinks.Count);

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

                OverlappedLocationView overlapLocation = new OverlappedLocationView(linkLoc, circle, true);
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

                OverlappedLocationView overlapLocation = new OverlappedLocationView(linkLoc, circle, false);
                listCircles.Add(overlapLocation);
            }

            return listCircles;
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          OverlappedLinkCircleView[] listToDraw)
        {
            OverlappedLocationView[] linkViewArray = listToDraw.SelectMany(l => l.linkViews).ToArray();
            OverlappedLocationView.Draw(device, scene, basicEffect, overlayEffect, linkViewArray);
        }

        public void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              VikingXNA.Scene scene)
        {

            foreach (OverlappedLocationView ov in this.linkViews)
            {
                if (!ov.IsVisible(scene))
                    return;

                ov.DrawLabel(spriteBatch, font, scene, 0);
            }

            return;
        }
    }
}
