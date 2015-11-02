using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Geometry;
using WebAnnotationModel;
using WebAnnotation;
using VikingXNAGraphics;
using WebAnnotation.ViewModel;
using WebAnnotation.UI.Commands;

namespace WebAnnotation.View
{
    class LocationCircleView : LocationCanvasView
    {
        #region static

        static double BeginFadeCutoff = 0.1;
        static double InvisibleCutoff = 1f;
        static float LabelVisibleCutoff = 7f;
        static float RadiusToResizeCircle = 7.0f / 8.0f;
        static float RadiusToLinkCircle = 1.0f / 4.0f;

        #endregion

        public LocationCircleView(LocationObj obj) : base(obj)
        { }

        #region overrides

        public override GridRectangle BoundingBox
        {
            get
            {
                return Circle.BoundingBox;
            }
        }

        /// <summary>
        /// True if the point is on or inside the circle
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override bool Intersects(GridVector2 Position)
        {
            return Circle.Contains(Position);
        }

        /// <summary>
        /// Distance to the nearest point on circle if outside, otherwise zero
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override double Distance(GridVector2 Position)
        {
            double Distance = GridVector2.Distance(Position, this.Circle.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }


        public override bool IsVisible(VikingXNA.Scene scene)
        {
            double maxDimension = Math.Max(scene.VisibleWorldBounds.Width, scene.VisibleWorldBounds.Height);
            double LocToScreenRatio = Radius * 2 / maxDimension;
            if (LocToScreenRatio > InvisibleCutoff)
                return false;

            double maxPixelDimension = Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight);
            if (Radius * 2 <= maxPixelDimension)
                return false;

            return true;
        }

        public override LocationAction GetActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            double distance = this.Distance(WorldPosition);

            if (distance > this.Radius)
                return LocationAction.NONE;
            else if (distance >= (this.Radius * RadiusToResizeCircle))
                return LocationAction.SCALE;
            else if (distance >= (this.Radius * RadiusToLinkCircle))
                return LocationAction.CREATELINK;
            else
                return LocationAction.TRANSLATE;
        }

        #endregion

        public GridCircle Circle
        {
            get
            {
                return new GridCircle(this.VolumePosition, this.Radius);
            }
        }


        public double Radius
        {
            get
            {
                return modelObj.Radius;
            }
        }

        #region Render Code
        public VertexPositionColorTexture[] _BackgroundVerts = null;
        public VertexPositionColorTexture[] BackgroundVerts
        {
            get
            {
                if (_BackgroundVerts == null)
                {
                    RegisterForLocationEvents();
                    _BackgroundVerts = new VertexPositionColorTexture[GlobalPrimitives.SquareVerts.Length];
                    GlobalPrimitives.SquareVerts.CopyTo(_BackgroundVerts, 0);

                    //Size the background verts correctly
                    for (int i = 0; i < _BackgroundVerts.Length; i++)
                    {
                        _BackgroundVerts[i].Position *= (float)this.Radius;
                        _BackgroundVerts[i].Position.X += (float)VolumePosition.X;
                        _BackgroundVerts[i].Position.Y += (float)VolumePosition.Y;
                    }
                }

                return _BackgroundVerts;
            }
        }

        public VertexPositionColorTexture[] _AboveSectionBackgroundVerts = null;
        public VertexPositionColorTexture[] AboveSectionBackgroundVerts
        {
            get
            {
                if (_AboveSectionBackgroundVerts == null)
                {
                    RegisterForLocationEvents();
                    _AboveSectionBackgroundVerts = new VertexPositionColorTexture[GlobalPrimitives.SquareVerts.Length];
                    GlobalPrimitives.SquareVerts.CopyTo(_AboveSectionBackgroundVerts, 0);

                    //Size the background verts correctly
                    for (int i = 0; i < _AboveSectionBackgroundVerts.Length; i++)
                    {
                        _AboveSectionBackgroundVerts[i].Position *= (float)this.OffSectionRadius;
                        _AboveSectionBackgroundVerts[i].Position.X += (float)VolumePosition.X;
                        _AboveSectionBackgroundVerts[i].Position.Y += (float)VolumePosition.Y;
                    }
                }

                return _AboveSectionBackgroundVerts;
            }
        }

        public VertexPositionColorTexture[] _BelowSectionBackgroundVerts = null;
        public VertexPositionColorTexture[] BelowSectionBackgroundVerts
        {
            get
            {
                if (_BelowSectionBackgroundVerts == null)
                {
                    RegisterForLocationEvents();
                    _BelowSectionBackgroundVerts = new VertexPositionColorTexture[GlobalPrimitives.SquareVerts.Length];
                    GlobalPrimitives.SquareVerts.CopyTo(_BelowSectionBackgroundVerts, 0);

                    //Size the background verts correctly
                    for (int i = 0; i < _BelowSectionBackgroundVerts.Length; i++)
                    {
                        _BelowSectionBackgroundVerts[i].Position *= (float)this.OffSectionRadius;
                        _BelowSectionBackgroundVerts[i].Position.X += (float)VolumePosition.X;
                        _BelowSectionBackgroundVerts[i].Position.Y += (float)VolumePosition.Y;

                        _BelowSectionBackgroundVerts[i].TextureCoordinate = new Vector2(_BelowSectionBackgroundVerts[i].TextureCoordinate.X == 0 ? 1 : 0,
                                                                                        _BelowSectionBackgroundVerts[i].TextureCoordinate.Y == 0 ? 1 : 0);
                    }
                }

                return _BelowSectionBackgroundVerts;
            }
        }

        /// <summary>
        /// Get the verts we need rendered for this location so they can be batched into one draw call
        /// </summary>
        /// <param name="DirectionToVisiblePlane"></param>
        /// <returns></returns>
        public VertexPositionColorTexture[] GetBackgroundVerticies(GridRectangle VisibleBounds, double Downsample, int DirectionToVisiblePlane, out int[] indicies)
        {
            StructureTypeObj type = this.Parent.Type.modelObj;
            Microsoft.Xna.Framework.Color color = type.Color.ToXNAColor(1.0f);
            Microsoft.Xna.Framework.Color HSLColor = color.ConvertToHSL();

            //Alpha modifies how much the value of the overlay texture is mixed with the background texture.
            //Saturation can be used to make the annotation not change the background
            byte alpha = 128;
            float SatScalar = HSLColor.B / 255.0f;
            if (AnnotationOverlay.LastMouseOverObject == this)
            {
                alpha = 32;
                SatScalar = 0.25f;
            }
            else if (DirectionToVisiblePlane != 0)
            {
                alpha = 64;
                SatScalar = 0.5f;
            }

            double maxDimension = Math.Max(VisibleBounds.Width, VisibleBounds.Height);

            double LocToScreenRatio = Radius * 2 / maxDimension;
            SatScalar *= Viking.Common.Util.GetFadeFactor(LocToScreenRatio, BeginFadeCutoff, InvisibleCutoff);

            HSLColor.A = alpha;
            HSLColor.G = (Byte)((float)HSLColor.G * SatScalar);
            //HSLColor.B = (Byte)((float)HSLColor.B * SatScalar);

            return GetCircleBackgroundVerts(DirectionToVisiblePlane, HSLColor, out indicies);
        }

        /// <summary>
        /// The verticies should really be cached and handed up to LocationObjRenderer so all similiar objects can be rendered in one
        /// call.  This method is in the middle of a change from using triangles to draw circles to using textures. 
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="DirectionToVisiblePlane"></param>
        /// <param name="color"></param>
        public VertexPositionColorTexture[] GetCircleBackgroundVerts(int DirectionToVisiblePlane, Microsoft.Xna.Framework.Color HSLColor, out int[] indicies)
        {
            //            GridVector2 Pos = this.VolumePosition;

            //Can't populate until we've referenced CircleVerts
            indicies = GlobalPrimitives.SquareIndicies;
            //            float radius = (float)this.Radius;

            VertexPositionColorTexture[] verts;

            if (DirectionToVisiblePlane == 0)
                verts = BackgroundVerts;
            else if (DirectionToVisiblePlane < 0)
                verts = AboveSectionBackgroundVerts;
            else
                verts = BelowSectionBackgroundVerts;


            //Draw an opaque border around the background
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Color = HSLColor;
            }

            return verts;
        }
        #endregion


        #region Linked Locations


        public VertexPositionColorTexture[] GetLinkedLocationBackgroundVerts(GridRectangle VisibleBounds, double downsample, out int[] indicies)
        {
            double maxDimension = Math.Max(VisibleBounds.Width, VisibleBounds.Height);
            double SatScalar = 0.5;
            double LocToScreenRatio = Radius * 2 / maxDimension;

            if (LocToScreenRatio > BeginFadeCutoff)
            {
                SatScalar *= Viking.Common.Util.GetFadeFactor(LocToScreenRatio, BeginFadeCutoff, InvisibleCutoff);
            }

            StructureTypeObj type = this.modelObj.Parent.Type;
            Microsoft.Xna.Framework.Color selectedColor = type.Color.ToXNAColor(0.5f);
            Microsoft.Xna.Framework.Color unselectedColor = type.Color.ToXNAColor(0.125f);

            Microsoft.Xna.Framework.Color selectedHSLColor = selectedColor.ConvertToHSL();
            Microsoft.Xna.Framework.Color unselectedHSLColor = unselectedColor.ConvertToHSL();

            selectedHSLColor.G = (byte)((float)selectedHSLColor.G * SatScalar);
            unselectedHSLColor.G = (byte)((float)unselectedHSLColor.G * SatScalar);

            return GetLinkedLocationBackgroundVerts(downsample, selectedHSLColor, unselectedHSLColor, out indicies);
        }

        /// <summary>
        /// Artificially positioned circles over our annotation circle which indicate links to locations on adjacent sections overlapped by this location
        /// </summary>
        private ConcurrentDictionary<OverlappedLocation, GridCircle> _OverlappingLinkedLocationCircles = null;
        public ConcurrentDictionary<OverlappedLocation, GridCircle> OverlappingLinkedLocationCircles
        {
            get
            {
                if (_OverlappingLinkedLocationCircles == null)
                {
                    RegisterForLocationEvents();
                    _OverlappingLinkedLocationCircles = CalculateOverlappedLocationCircles();
                    NotifyCollectionChangedEventManager.AddListener(this.modelObj.Links, this);
                }
                /*
            else
            {
                if (Links != null)
                { 
                    _OverlappingLinkedLocationCircles = CalculateOverlappedLocationCircles();
                    _OverlappingLinkedLocationVerts = null;
                    _OverlappingLinkedLocationIndicies = null; 
                }
            }
            */

                return _OverlappingLinkedLocationCircles;
            }
        }

        private VertexPositionColorTexture[] _OverlappingLinkedLocationVerts = null;
        private int[] _OverlappingLinkedLocationIndicies = null;

        private List<LocationCanvasView> _OverlappingLinks = null;
        public override IList<LocationCanvasView> OverlappingLinks
        {
            get
            {
                if (_OverlappingLinks == null)
                {
                    _OverlappingLinks = CalculateOverlappingLinks(this.Links);
                }

                return _OverlappingLinks;
            }
        }

        /// <summary>
        /// Given a list of ID's, return a list of Locations which are overlapping our canvas model
        /// </summary>
        /// <param name="linkedLocations"></param>
        /// <returns></returns>
        public List<LocationCanvasView> CalculateOverlappingLinks(ICollection<long> linkedIDs)
        {
            IEnumerable<LocationObj> listLinkedLocations = Store.Locations.GetObjectsByIDs(linkedIDs, false);

            listLinkedLocations = listLinkedLocations.Where(loc => loc.Z != this.Z);
            IEnumerable<LocationCanvasView> listCanvasLocations = listLinkedLocations.Select(loc => AnnotationViewFactory.Create(loc));

            return listCanvasLocations.Where(loc => loc.VolumeShape.STIntersects(this.VolumeShape).IsTrue).ToList();
        }

        /// <summary>
        /// A linked location overlapping with our location is drawn as a small circle.  This function stores the position of those smaller circles along an arc
        /// </summary>
        /// <returns></returns>
        private ConcurrentDictionary<OverlappedLocation, GridCircle> CalculateOverlappedLocationCircles()
        {
            ConcurrentDictionary<OverlappedLocation, GridCircle> listCircles = null;
            if (_OverlappingLinkedLocationCircles == null)
            {
                listCircles = new ConcurrentDictionary<OverlappedLocation, GridCircle>();
            }
            else
            {
                listCircles = _OverlappingLinkedLocationCircles;
                listCircles.Clear();
            }

            List<LocationCanvasView> listLinksAbove = this.OverlappingLinks.Where(loc => loc.Z > this.Z).ToList();
            List<LocationCanvasView> listLinksBelow = this.OverlappingLinks.Where(loc => loc.Z < this.Z).ToList();

            listLinksAbove = listLinksAbove.OrderBy(L => -L.VolumePosition.X).ThenBy(L => L.VolumePosition.Y).ToList();
            listLinksBelow = listLinksBelow.OrderBy(L => L.VolumePosition.X).ThenBy(L => L.VolumePosition.Y).ToList();

            //Figure out how large link images would be
            double linkRadius = this.Radius / 6;

            double linkArcNormalizedDistanceFromCenter = 0.75;
            double linkArcDistanceFromCenter = linkArcNormalizedDistanceFromCenter * this.Radius;
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

            //double angleOffset =((listLinksAbove.Count / 2) / (double)listLinksAbove.Count) * Math.PI;
            double halfNumLinksAbove = listLinksAbove.Count / 2;
            double angleOffset = ((double)(1 - listLinksAbove.Count) % 2) * (UpperArcStepSize / 2);
            for (int iLocAbove = 0; iLocAbove < listLinksAbove.Count; iLocAbove++)
            {
                LocationCanvasView linkLoc = listLinksAbove[iLocAbove];

                //Figure out where the link should be drawn. 
                //Allocate the top 180 degree arc for links above, the bottom 180 for links below

                double angle = (((((double)iLocAbove - halfNumLinksAbove) * UpperArcStepSize) - angleOffset) * Math.PI); //- angleOffset;

                Vector3 positionOffset = new Vector3((float)Math.Sin(angle), (float)Math.Cos(angle), (float)0);
                positionOffset *= (float)linkArcDistanceFromCenter;

                GridCircle circle = new GridCircle(this.VolumePosition + new GridVector2(positionOffset.X, positionOffset.Y), UpperArcLinkRadius);

                OverlappedLocation overlapLocation = new OverlappedLocation(linkLoc.modelObj, new LocationLinkView(this.modelObj, linkLoc.modelObj), circle);
                bool added = listCircles.TryAdd(overlapLocation, circle);
                if (!added)
                {
                    //overlapLocation = null;
                    linkLoc = null;
                }
            }

            double halfNumLinksBelow = listLinksBelow.Count / 2;
            angleOffset = ((double)(1 - listLinksBelow.Count) % 2) * (LowerArcStepSize / 2);
            for (int iLocBelow = 0; iLocBelow < listLinksBelow.Count; iLocBelow++)
            {
                LocationCanvasView linkLoc = listLinksBelow[iLocBelow];

                //Figure out where the link should be drawn. 
                //Allocate the top 180 degree arc for links above, the bottom 180 for links below

                double angle = (((((double)iLocBelow - halfNumLinksBelow) * LowerArcStepSize) - angleOffset) * Math.PI) + Math.PI;

                Vector3 positionOffset = new Vector3((float)Math.Sin(angle), (float)Math.Cos(angle), (float)0);
                positionOffset *= (float)linkArcDistanceFromCenter;

                GridCircle circle = new GridCircle(this.VolumePosition + new GridVector2(positionOffset.X, positionOffset.Y), LowerArcLinkRadius);

                OverlappedLocation overlapLocation = new OverlappedLocation(linkLoc.modelObj, new LocationLinkView(this.modelObj, linkLoc.modelObj), circle);
                bool added = listCircles.TryAdd(overlapLocation, circle);
                if (!added)
                {
                    //overlapLocation = null;
                    linkLoc = null;
                }
            }

            return listCircles;
        }

        /// <summary>
        /// When locations are very small we should not bother rendering text
        /// </summary>
        /// <param name="Downsample"></param>
        /// <returns></returns>
        public bool OverlappingLocationLinksCanBeSeen(double downsample)
        {
            return this.Radius / downsample >= 64;
        }

        /// <summary>
        /// A circle location can have a number of location links embedded as smaller circles.  This function 
        /// indicates whether a linked location is under the mouse or the main annotation
        /// </summary>
        /// <param name="position"></param>
        public LocationCanvasView GetLocationUnderPosition(GridVector2 position, out double distanceToCenter)
        {
            distanceToCenter = double.MaxValue;
            foreach (KeyValuePair<OverlappedLocation, GridCircle> Item in this.OverlappingLinkedLocationCircles)
            {
                if(Item.Value.Contains(position))
                {
                    distanceToCenter = GridVector2.Distance(position, Item.Value.Center);
                    return Item.Key;
                }
            }

            if(this.Intersects(position))
            {
                distanceToCenter = GridVector2.Distance(this.Circle.Center, position);
                return this;
            }

            return null; 
        }

        public VertexPositionColorTexture[] GetLinkedLocationBackgroundVerts(double downsample,
                                                                             Microsoft.Xna.Framework.Color unselectedColor,
                                                                             Microsoft.Xna.Framework.Color selectionColor,
                                                                             out int[] indicies)
        {
            indicies = _OverlappingLinkedLocationIndicies;

            //Figure out if we are too small to display location link icons
            if (!OverlappingLocationLinksCanBeSeen(downsample))
            {
                //Free the memory just in case
                _OverlappingLinkedLocationVerts = null;
                _OverlappingLinkedLocationIndicies = null;

                return new VertexPositionColorTexture[0];
            }

            if (_OverlappingLinkedLocationCircles != null)
            {
                if (_OverlappingLinkedLocationCircles.Count != Links.Count)
                {
                    _OverlappingLinkedLocationCircles = CalculateOverlappedLocationCircles();
                    _OverlappingLinkedLocationVerts = null;
                    _OverlappingLinkedLocationIndicies = null;
                }
            }

            OverlappedLocation overlapLocation = AnnotationOverlay.LastMouseOverObject as OverlappedLocation;
            if (overlapLocation != null)
            {
                //Redo our verticies if we are
                if (overlapLocation.link.A.ID == this.ID || overlapLocation.link.B.ID == this.ID)
                {
                    _OverlappingLinkedLocationCircles = CalculateOverlappedLocationCircles();
                    _OverlappingLinkedLocationVerts = null;
                    _OverlappingLinkedLocationIndicies = null;
                }
            }

            if (_OverlappingLinkedLocationVerts != null)
            {
                for (int i = 0; i < _OverlappingLinkedLocationVerts.Length; i++)
                {
                    _OverlappingLinkedLocationVerts[i].Color = unselectedColor;
                }

                return _OverlappingLinkedLocationVerts;
            }

            //If we've already calculated the verticies use those
            if (_OverlappingLinkedLocationVerts == null)
            {
                VertexPositionColorTexture[] Verts = new VertexPositionColorTexture[OverlappingLinkedLocationCircles.Count * GlobalPrimitives.SquareVerts.Length];
                indicies = new int[OverlappingLinkedLocationCircles.Count * GlobalPrimitives.SquareIndicies.Length];

                int iVert = 0;
                int iIndex = 0;
                foreach (KeyValuePair<LocationObj, GridCircle> Item in this.OverlappingLinkedLocationCircles)
                {
                    GridCircle locCircle = Item.Value;
                    LocationObj linkedLoc = Item.Key;

                    //Make sure our verts and location lengths still agree, sometimes the collection size can grow.
                    if (iVert >= Verts.Length)
                    {
                        VertexPositionColorTexture[] VertsTemp = new VertexPositionColorTexture[OverlappingLinkedLocationCircles.Count * GlobalPrimitives.SquareVerts.Length];
                        Verts.CopyTo(VertsTemp, 0);
                        Verts = VertsTemp;
                        VertsTemp = null;

                        int[] indiciesTemp = new int[OverlappingLinkedLocationCircles.Count * GlobalPrimitives.SquareIndicies.Length];
                        indicies.CopyTo(indiciesTemp, 0);
                        indicies = indiciesTemp;
                        indiciesTemp = null;
                    }

                    GlobalPrimitives.SquareVerts.CopyTo(Verts, iVert);
                     
                    Microsoft.Xna.Framework.Color color = (linkedLoc == AnnotationOverlay.LastMouseOverObject as LocationObj) ? selectionColor : unselectedColor;

                    bool invertTexture = linkedLoc.Z - this.Z < 0;

                    //Scale, translate, and color the background verts correctly
                    for (int i = 0; i < GlobalPrimitives.SquareVerts.Length; i++)
                    {
                        Verts[i + iVert].Position *= (float)locCircle.Radius;
                        Verts[i + iVert].Position.X += (float)locCircle.Center.X;
                        Verts[i + iVert].Position.Y += (float)locCircle.Center.Y;
                        Verts[i + iVert].Color = color;

                        if (invertTexture)
                        {
                            Verts[i + iVert].TextureCoordinate.Y = 1 - Verts[i + iVert].TextureCoordinate.Y;
                        }
                    }

                    for (int i = 0; i < GlobalPrimitives.SquareIndicies.Length; i++)
                    {
                        indicies[iIndex + i] = GlobalPrimitives.SquareIndicies[i] + iVert;
                    }

                    iVert += GlobalPrimitives.SquareVerts.Length;
                    iIndex += GlobalPrimitives.SquareIndicies.Length;

                    //                    Location.AppendVertLists(linkVerts, listVerts, GlobalPrimitives.SquareIndicies, ref listIndicies);
                }

                _OverlappingLinkedLocationVerts = Verts;
                _OverlappingLinkedLocationIndicies = indicies;
            }

            return _OverlappingLinkedLocationVerts;

        }

        #endregion

        #region Label

        private bool _LabelSizeMeasured = false;
        private Vector2 _LabelSize;

        private string _InfoLabelMeasured = "";
        private bool _InfoLabelSizeMeasured = false;
        private string[] _InfoLabelAfterSplit = new string[0];
        private Vector2[] _InfoLabelSize = new Vector2[0];

        private bool _ParentLabelSizeMeasured = false;
        private Vector2 _ParentLabelSize;

        public Vector2 GetLabelSize()
        {
            if (_LabelSizeMeasured)
                return _LabelSize;

            //Otherwise we aren't really sure about the label size, so just guess
            return new Vector2(256f, 128f);
        }

        public Vector2 GetLabelSize(SpriteFont font)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (_LabelSizeMeasured)
                return _LabelSize;

            string label = modelObj.Label;
            //Label can't be empty or the offset measured is zero
            if (String.IsNullOrEmpty(label))
                label = " ";

            _LabelSize = font.MeasureString(label);
            _LabelSizeMeasured = true;
            return _LabelSize;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="font"></param>
        /// <param name="nLinesAllowed"></param>
        /// <param name="LabelY">The y elevation of the first line of the label on a unit circle.  Using this we calculate how much room we have in X</param>
        /// <param name="LineSpacing">The y distance used for each line on a unit circle</param>
        /// <returns></returns>
        public Vector2[] GetInfoLabelSizeOnCircle(string NewInfoLabel, SpriteFont font, float Scale)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (_InfoLabelSizeMeasured && _InfoLabelMeasured == NewInfoLabel)
                return _InfoLabelSize;

            _InfoLabelMeasured = NewInfoLabel;

            if (modelObj.Parent == null)
                return new Vector2[] { new Vector2(0, 0) };

            string text = NewInfoLabel;
            //Label can't be empty or the offset measured is zero
            if (String.IsNullOrEmpty(text))
                text = " ";

            Vector2 FullLabelSize = font.MeasureString(text);
            //FullLabelSize *= InfoLabelToLabelSizeRatio; 

            if (FullLabelSize.X > this.Radius * 0.5f)
            {
                this._InfoLabelAfterSplit = LocationObjRenderer.SplitLabel(text);
            }
            else
            {
                _InfoLabelAfterSplit = new string[] { text };
            }

            this._InfoLabelSize = font.MeasureStrings(_InfoLabelAfterSplit);

            _InfoLabelSizeMeasured = true;
            return _InfoLabelSize;
        }

        public Vector2 GetParentLabelSize(SpriteFont font)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (_ParentLabelSizeMeasured)
                return _ParentLabelSize;

            if (modelObj.Parent == null)
                return new Vector2(0, 0);

            if (modelObj.Parent.Parent == null)
                return new Vector2(0, 0);


            string label = modelObj.Parent.Parent.ToString();
            //Label can't be empty or the offset measured is zero
            if (String.IsNullOrEmpty(label))
                label = " ";

            _ParentLabelSize = font.MeasureString(label);
            _ParentLabelSizeMeasured = true;
            return _LabelSize;
        }


        /// <summary>
        /// Draw the text for the location at the specified screen coordinates
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="ScreenDrawPosition">Center of the annotation in screen space, which is the coordinate system used for text</param>
        /// <param name="MagnificationFactor"></param>
        /// <param name="DirectionToVisiblePlane">The Z distance of the location to the plane viewed by user.</param>
        public override void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              Vector2 LocationCenterScreenPosition,
                              float MagnificationFactor,
                              int DirectionToVisiblePlane)
        {
            if (font == null)
                throw new ArgumentNullException("font");

            if (spriteBatch == null)
                throw new ArgumentNullException("spriteBatch");

            const byte DefaultAlpha = 192;
            //Labels draw at the top left, so we have to offset the drawstring call so the label is centered on the annotation
            Vector2 offset = GetLabelSize(font);
            offset.X /= 2;
            offset.Y /= 2;
            //Offset.x is now the amount to subtract from the label to center it on the annotation


            bool UsingArtificialRadiusForLowMag = false;

            //Scale is used to adjust for the magnification factor of the viewer.  Otherwise text would remain at constant size regardless of mag factor.
            //offsets must be multiplied by scale before use
            float baseScale = BaseFontSizeForLocationType(modelObj.TypeCode, DirectionToVisiblePlane, MagnificationFactor, font);  //The base scale used for Label text, adjusted for additional info text
            bool LowMagScale = ScaleReducedForLowMag(baseScale);

            //Don't draw labels if no human could read them
            if (LabelIsTooSmallToSee(baseScale, font.LineSpacing))
                return;

            StructureType type = this.Parent.Type;

            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color((byte)(0),
                                                                                    (byte)(0),
                                                                                    (byte)(0),
                                                                                    DefaultAlpha);

            float labelScale = baseScale;
            if (this.modelObj.Equals(CreateNewLinkedLocationCommand.LastEditedLocation))
            {
                double alpha = (DateTime.UtcNow.Millisecond / 1000.0) * Math.PI;
                alpha = Math.Sin(alpha);
                color = new Microsoft.Xna.Framework.Color((byte)(type.Color.R / 4),
                                                          (byte)(type.Color.G / 4),
                                                          (byte)(type.Color.B / 4),
                                                          (byte)(alpha * 255));

                labelScale = baseScale + (baseScale * (float)alpha * .25f);
            }
            else if (modelObj.OffEdge)
            {
                color = new Microsoft.Xna.Framework.Color(255, 255, 255, 128);
            }
            else if (modelObj.IsUnverifiedTerminal)
            {
                color = new Microsoft.Xna.Framework.Color((byte)((255 - type.Color.R) / 1),
                                                          (byte)((255 - type.Color.G) / 1),
                                                          (byte)((255 - type.Color.B) / 1),
                                                          (byte)128);
            }


            byte scaledAlpha = color.A;
            if (!UsingArtificialRadiusForLowMag && !LowMagScale)
            {
                scaledAlpha = (byte)(GetAlphaForScale(baseScale, (float)color.A / 255) * 255.0);
                color.A = scaledAlpha;
            }

            //If we have a parent of our parent (Child of a structure, such as a synapse) then include thier ID in small font
            if (this.Parent.Parent != null)
            {
                StructureType ParentType = this.Parent.Parent.Type;
                Vector2 ParentOffset = this.GetParentLabelSize(font);
                ParentOffset.X /= 2f;
                ParentOffset.Y /= 2f;

                //                string ParentLabel = this.Parent.Parent.ToString();
                float ParentScale = baseScale / 1.75f;

                if (LabelIsTooSmallToSee(ParentScale, font.LineSpacing))
                    return;

                Microsoft.Xna.Framework.Color ParentColor = new Microsoft.Xna.Framework.Color(ParentType.Color.R,
                                                                                              ParentType.Color.G,
                                                                                              ParentType.Color.B,
                                                                                              GetAlphaForScale(ParentScale, 0.5f));

                Microsoft.Xna.Framework.Color LabelColor = new Microsoft.Xna.Framework.Color(0f,
                                                                                              0f,
                                                                                              0f,
                                                                                              GetAlphaForScale(ParentScale, 0.5f));

                if (LabelColor.A > 0)
                {
                    Vector2 LabelScreenDrawPosition = LocationCenterScreenPosition;



                    //Position label below the label for the location
                    LabelScreenDrawPosition.Y += ((offset.Y * 3f) * ParentScale);
                    LabelScreenDrawPosition.X -= (offset.X / 2) * ParentScale;


                    spriteBatch.DrawString(font,
                                            this.modelObj.Label,
                                            LabelScreenDrawPosition,
                                            LabelColor,
                                            0,
                                            ParentOffset,
                                            ParentScale,
                                            SpriteEffects.None,
                                            0);
                }

                if (ParentColor.A > 0)
                {
                    Vector2 ParentScreenDrawPosition = LocationCenterScreenPosition;

                    //Position parent label above the label for the location
                    ParentScreenDrawPosition.Y -= ((ParentOffset.Y * 3f) * ParentScale);
                    ParentScreenDrawPosition.X -= 0;// (ParentOffset.X) * ParentScale;

                    spriteBatch.DrawString(font,
                                            Parent.Parent.ToString(),
                                            ParentScreenDrawPosition,
                                            ParentColor,
                                            0,
                                            ParentOffset,
                                            ParentScale,
                                            SpriteEffects.None,
                                            0);
                }
            }
            else
            {
                if (color.A > 0)
                {
                    Vector2 LabelDrawPosition = LocationCenterScreenPosition;
                    LabelDrawPosition.Y += (font.LineSpacing * 0.66f) * baseScale;
                    //       LabelDrawPosition.X -= offset.X * baseScale;

                    spriteBatch.DrawString(font,
                                            this.modelObj.Label,
                                            LabelDrawPosition,
                                            color,
                                            0,
                                            offset,
                                            labelScale,
                                            SpriteEffects.None,
                                            0);
                }

            }


            DrawStructAndTagLabel(spriteBatch, font, LocationCenterScreenPosition, color, baseScale, DirectionToVisiblePlane);

            if (DirectionToVisiblePlane == 0)
            {
                float AlphaForSectionLabels = 0;

                //Indicate the z value for each adjacent location
                foreach (KeyValuePair<OverlappedLocation, GridCircle> adjLoc in this.OverlappingLinkedLocationCircles)
                {
                    string infoLabel = adjLoc.Key.Z.ToString();
                    float scale = (float)((adjLoc.Value.Radius / font.LineSpacing) / 2) * MagnificationFactor;
                    float LineStep = font.LineSpacing * scale;
                    //Info labels are smaller than main labels, so make sure they can be seen
                    if (LineStep < LabelVisibleCutoff)
                    {
                        break;
                    }

                    if (AlphaForSectionLabels == 0)
                    {
                        AlphaForSectionLabels = GetAlphaForScale(scale, DefaultAlpha);
                        if (AlphaForSectionLabels <= 0)
                        {
                            break;
                        }
                    }

                    Microsoft.Xna.Framework.Color InfoLabelColor = new Microsoft.Xna.Framework.Color(1.0f, 0, 0, AlphaForSectionLabels / 255f);

                    Vector2[] labelSizeArray = adjLoc.Key.GetInfoLabelSizeOnCircle(infoLabel, font, scale);

                    float yOffset = (LineStep / 3) * 2;
                    //float yOffset = 0; 

                    for (int iLine = 0; iLine < labelSizeArray.Length; iLine++)
                    {
                        //Get label string and offset for this line
                        string AdditionalLabel = adjLoc.Key._InfoLabelAfterSplit[iLine];
                        Vector2 labelOffset = labelSizeArray[iLine];

                        labelOffset.X /= 2; //Center the label on the annotation

                        //Position label below the label for the location
                        GridVector2 ScreenPosition = AnnotationOverlay.CurrentOverlay.Parent.WorldToScreen(adjLoc.Value.Center.X,
                                                                                                     adjLoc.Value.Center.Y);

                        Vector2 DrawPosition = new Vector2((float)ScreenPosition.X, (float)ScreenPosition.Y);

                        //DrawPosition.Y += labelOffset.Y/4 * scale;
                        DrawPosition.Y += yOffset;
                        DrawPosition.X += (labelOffset.X * scale);

                        spriteBatch.DrawString(font,
                            AdditionalLabel,
                            DrawPosition,
                            InfoLabelColor,
                            0,
                            labelSizeArray[iLine],
                            scale,
                            SpriteEffects.None,
                            0);

                        yOffset += LineStep;
                    }
                }
            }
        }

        /// <summary>
        /// Full label and tag text
        /// </summary>
        /// <returns></returns>
        private string FullLabelText(int DirectionToVisiblePlane)
        {
            string fullLabel = this.StructureLabel(DirectionToVisiblePlane);

            if (fullLabel.Length == 0)
                fullLabel = this.TagLabel(DirectionToVisiblePlane);
            else
                fullLabel += '\n' + this.TagLabel(DirectionToVisiblePlane);

            return fullLabel;
        }

        private void DrawStructAndTagLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                                            Microsoft.Xna.Framework.Graphics.SpriteFont font,
                                            Vector2 LocationCenterScreenPosition,
                                            Microsoft.Xna.Framework.Color color,
                                            float baseScale,
                                            int DirectionToVisiblePlane)
        {
            //Draw the info label. If we are drawing an off-section location, then indicate section number.  Otherwise use the parent label
            if (color.A <= 0)
                return;

            string fullLabelText = this.FullLabelText(DirectionToVisiblePlane);

            if (fullLabelText.Length > 0)
            {
                float InfoToLabelRatio = 1 / 2.5f;
                float InfoLabelScale = baseScale * InfoToLabelRatio;

                Vector2[] LabelOffsetArray = GetInfoLabelSizeOnCircle(fullLabelText, font, InfoLabelScale);

                if (!LabelIsTooSmallToSee(InfoLabelScale, font.LineSpacing))
                {
                    float LineStep = font.LineSpacing * InfoLabelScale;  //How much do we increment Y to move down a line?
                    float yOffset = -(font.LineSpacing * 0.66f) * InfoLabelScale;  //What is the offset to draw the line at the correct position?  We have to draw below label if it exists
                    //However we only need to drop half a line since the label straddles the center

                    Microsoft.Xna.Framework.Color InfoLabelColor = color;

                    if (DirectionToVisiblePlane != 0)
                    {
                        InfoLabelColor = new Microsoft.Xna.Framework.Color(255, 0, 0, color.A);
                    }
                    // InfoLabelColor.A = (byte)(GetAlphaForScale(InfoScale, 0.5f) * 255);

                    for (int iLine = 0; iLine < LabelOffsetArray.Length; iLine++)
                    {
                        //Get label string and offset for this line
                        string AdditionalLabel = _InfoLabelAfterSplit[iLine];
                        Vector2 labelOffset = LabelOffsetArray[iLine];

                        labelOffset.X /= 2; //Center the label on the annotation

                        //Position label below the label for the location
                        Vector2 DrawPosition = LocationCenterScreenPosition;
                        //DrawPosition.Y += labelOffset.Y/4 * scale;
                        DrawPosition.Y += yOffset;
                        DrawPosition.X += (labelOffset.X * InfoLabelScale);

                        spriteBatch.DrawString(font,
                            AdditionalLabel,
                            DrawPosition,
                            InfoLabelColor,
                            0,
                            LabelOffsetArray[iLine],
                            InfoLabelScale,
                            SpriteEffects.None,
                            0);

                        yOffset += LineStep;
                    }
                }
            }

        }


        private float GetAlphaForScale(float scale, float ViewingDistanceAlpha)
        {
            return GetAlphaForScale(scale, ViewingDistanceAlpha, 1f, 0f, 0.05f, 2f, 0.6f);
        }

        private static float GetAlphaForScale(float scale, float OptimalViewingAlpha, float MaxAlpha, float MinAlpha, float opaqueBelowScaleCutoff, float InvisibleAboveScaleCutoff, float OptimalViewingScale)
        {
            //adjust alpha depending on zoom factor
            float scaledAlpha = OptimalViewingAlpha;
            if (scale < opaqueBelowScaleCutoff)
            {
                scaledAlpha = 1;
            }
            else if (scale > InvisibleAboveScaleCutoff)
            {
                scaledAlpha = MinAlpha;
            }
            else
            {
                if (scale == OptimalViewingScale)
                    scaledAlpha = OptimalViewingAlpha;
                else if (scale < OptimalViewingScale)
                {
                    float AvailableRange = 1 - OptimalViewingScale;
                    scaledAlpha = ((AvailableRange) * ((scale - opaqueBelowScaleCutoff) / (OptimalViewingScale - opaqueBelowScaleCutoff))) + OptimalViewingScale;
                }

                else
                {
                    scaledAlpha = (scaledAlpha - ((scale - OptimalViewingScale) * (scaledAlpha / InvisibleAboveScaleCutoff)));
                }
            }

            return scaledAlpha;
        }

        private static bool ScaleReducedForLowMag(float baseScale)
        {
            return baseScale < 1.0;
        }

        private float BaseFontSizeForLocationType(LocationType typecode, int DirectionToVisiblePlane, float MagnificationFactor, Microsoft.Xna.Framework.Graphics.SpriteFont font)
        {
            switch (typecode)
            {
                case LocationType.POINT: // a point
                    if (DirectionToVisiblePlane == 0)
                        return MagnificationFactor * AnnotationOverlay.LocationTextScaleFactor;
                    else
                        return MagnificationFactor * AnnotationOverlay.ReferenceLocationTextScaleFactor;
                case LocationType.CIRCLE: // a circle
                    if (DirectionToVisiblePlane == 0)
                    {
                        return (((float)Radius / (float)font.LineSpacing) * MagnificationFactor) / 2;
                    }
                    else
                    {
                        float maxLines = (float)this.OffSectionRadius / (float)font.LineSpacing;

                        return (maxLines * MagnificationFactor) / 2;
                    }
                default:
                    return MagnificationFactor * AnnotationOverlay.LocationTextScaleFactor;
            }
        }

        private static bool LabelIsTooSmallToSee(float baseScale, float LineSpacing)
        {
            return LineSpacing * baseScale < LocationCircleView.LabelVisibleCutoff;
        }

        private string StructureLabel(int DirectionToVisiblePlane)
        {
            if (DirectionToVisiblePlane == 0)
            {
                string InfoLabel = "";
                if (Parent.InfoLabel != null)
                    InfoLabel = Parent.InfoLabel.Trim();

                return InfoLabel;
            }
            else
            {
                return "z: " + this.Z.ToString();
            }
        }

        private string TagLabel(int DirectionToVisiblePlane)
        {

            if (DirectionToVisiblePlane != 0)
                return "";
            else
            {
                string InfoLabel = "";
                foreach (ObjAttribute tag in Parent.Attributes)
                {
                    InfoLabel += tag.ToString() + " ";
                }

                return InfoLabel.Trim();
            }
        }

#endregion


        /// <summary>
        /// How large is a locations radius on the panel when we are viewing an adjacent section 
        /// </summary>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static float CalcOffSectionRadius(double radius)
        {
            if (radius < 128f)
                return (float)radius;

            return (float)(radius / 2.0f < 64f ? 64f : radius / 2.0f);
        }

        /// <summary>
        /// The radius to use when the location is displayed as a reference location on another section
        /// </summary>
        public float OffSectionRadius
        {
            get
            {
                return CalcOffSectionRadius(this.Radius);
            }
        }

        protected override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label")
            {
                _LabelSizeMeasured = false;
                _InfoLabelSizeMeasured = false;
            }

            base.OnParentPropertyChanged(o, args);
        }

        protected override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            _BackgroundVerts = null;
            _AboveSectionBackgroundVerts = null;
            _BelowSectionBackgroundVerts = null;

            _OverlappingLinkedLocationCircles = null;
            _OverlappingLinkedLocationVerts = null;
            _OverlappingLinkedLocationIndicies = null;
        }

        protected override void OnLinksChanged(object o, NotifyCollectionChangedEventArgs args)
        {
            if (_OverlappingLinks != null)
                _OverlappingLinks.Clear();

            _OverlappingLinks = null;

            if (_OverlappingLinkedLocationCircles != null)
                _OverlappingLinkedLocationCircles.Clear();

            _OverlappingLinkedLocationCircles = null;

            _OverlappingLinkedLocationVerts = null;
            _OverlappingLinkedLocationIndicies = null;
        }

        
    }
}
