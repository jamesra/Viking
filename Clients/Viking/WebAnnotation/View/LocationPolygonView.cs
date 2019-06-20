using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using WebAnnotationModel;
using VikingXNAGraphics;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using VikingXNA;
using System.ComponentModel;
using WebAnnotation;
using Viking.VolumeModel;

namespace WebAnnotation.View
{
    class LocationPolygonView : LocationCanvasView, ILabelView, ICanvasViewContainer, Viking.Common.IHelpStrings, IColorView
    {
        private StructureCircleLabels curveLabels;
        private OverlappedLinkCircleView OverlappedLinkView;
         
        PositionColorMeshModel polygonMesh;

        GridPolygon VolumePolygon;
        GridPolygon SmoothedVolumePolygon;

        CircleView[] ControlPointViews = new CircleView[0];
        
        public override string[] HelpStrings
        {
            get
            {
                List<string> listStrings = new List<string>();
                listStrings.Add("Hold Left Click and drag the interior: Move shape");
                listStrings.Add("Hold Left Click and drag near edge: Create link");
                listStrings.Add("Shift + Left Click and drag: Move shape");
                listStrings.Add("Left click + CTRL on control point: Remove control point");
                listStrings.Add("Left click + CTRL on interior hole: Remove interior hole");
                listStrings.Add("Left click + CTRL off control point: Add a control point");
                listStrings.Add("Left click + CTRL inside shape: Cut hole in annotation");
                return listStrings.ToArray();
            }
        }

        private Color _Color; 

        public Microsoft.Xna.Framework.Color Color
        {
            get { return _Color; }
            set {
                _Color = value;
                if (polygonMesh != null)
                {
                    polygonMesh.Color = value;
                    ControlPointViews = CreateControlPointViews(VolumePolygon).ToArray();
                }
            }
        }

        public Microsoft.Xna.Framework.Color HSLColor
        {
            get { return _Color.ConvertToHSL(); } 
        }

        public float Alpha
        {
            get { return polygonMesh.Alpha; }
            set {
                polygonMesh.Alpha = value;
                ControlPointViews = CreateControlPointViews(VolumePolygon).ToArray();
            }
        }

        private double _ControlPointRadius;

        public double ControlPointRadius
        {
            get
            {
                return _ControlPointRadius;
            }
        }


        public double lineWidth = 32;

        public static uint NumInterpolationPoints = Global.NumClosedCurveInterpolationPoints;
        public LocationPolygonView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : base(obj)
        {
            _ControlPointRadius = Global.DefaultClosedLineWidth / 2.0;
            VolumePolygon = mapper.TryMapShapeSectionToVolume(obj.MosaicShape).ToPolygon();
            //_ControlPointRadius = GetRadiusFromPolygonArea(VolumePolygon, 0.01);
            SmoothedVolumePolygon = VolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            this.Color = obj.Parent == null ? Color.Gray.SetAlpha(0.5f) : obj.Parent.Type.Color.ToXNAColor(0.5f);
            polygonMesh = TriangleNetExtensions.CreateMeshForPolygon2D(SmoothedVolumePolygon, this.HSLColor);
            CreateLabelObjects();
            this.ControlPointViews = CreateControlPointViews(VolumePolygon).ToArray();
        }

        public static double GetRadiusFromPolygonArea(GridPolygon poly, double percentage)
        {
            double circleArea = poly.Area * percentage;
            double radius = Math.Sqrt(circleArea / Math.PI);
            return radius;
        }

        private GridCircle? _InscribedCircle;
        protected GridCircle InscribedCircle
        {
            get
            {
                if (!_InscribedCircle.HasValue)
                {
                    _InscribedCircle = SmoothedVolumePolygon.InscribedCircle();
                }

                return _InscribedCircle.Value;
            }
        }

        public void CreateLabelObjects()
        {
            curveLabels = new StructureCircleLabels(this.modelObj, this.InscribedCircle);
        }

        public List<CircleView> CreateControlPointViews(GridPolygon polygon)
        {
            List<CircleView> views = new List<CircleView>(polygon.ExteriorRing.Length);
            views.AddRange(polygon.ExteriorRing.Select(p => new CircleView(new GridCircle(p, ControlPointRadius), this.HSLColor.AdjustHSLHue(180))));

            foreach(GridPolygon innerPoly in polygon.InteriorPolygons)
            {
                views.AddRange(CreateControlPointViews(innerPoly));
            }

            return views;
        }
          
        private SqlGeometry _RenderedVolumeShape;
        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                if (_RenderedVolumeShape == null)
                {

                    _RenderedVolumeShape = modelObj.VolumeShape;// this.VolumeCurveControlPoints.ToPolyLine().STBuffer(this.Width / 2.0);                    
                }

                return _RenderedVolumeShape;
            }
        }

        /// <summary>
        /// We have this because with the current renderings the control points are circles that fall outside the polygon we use to render the closed curves
        /// </summary> 
        public override GridRectangle BoundingBox
        {
            get
            {
                return SmoothedVolumePolygon.BoundingBox.Pad(this._ControlPointRadius);
            }
        }

        public static void Draw(Microsoft.Xna.Framework.Graphics.GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundCurve.CurveManager lineManager,
                          Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect,
                          AnnotationOverBackgroundLumaEffect overlayEffect,
                          LocationPolygonView[] listToDraw)
        {
            OverlappedLinkCircleView[] overlappedLocations = listToDraw.Select(l => l.OverlappedLinkView).Where(l => l != null && l.IsVisible(scene)).ToArray();
            OverlappedLinkCircleView.Draw(device, scene, basicEffect, overlayEffect, overlappedLocations);

            CircleView.Draw(device, scene, basicEffect, overlayEffect, listToDraw.SelectMany(lpv => lpv.ControlPointViews).ToArray());
            //CurveView.Draw(device, scene, lineManager, basicEffect, overlayEffect, 0, listToDraw.Select(l => l.curveView).ToArray());

            MeshView<VertexPositionColor>.Draw(device, scene, listToDraw.Select(l => l.polygonMesh));
            //FilledClosedCurvePolygonView.Draw(device, scene, listToDraw.Select(l => l.polyView));
        }

        public override bool Intersects(GridVector2 Position)
        {
            if (!this.BoundingBox.Contains(Position))
                return false; 

            if (this.SmoothedVolumePolygon.ExteriorRing.Any(p => new GridCircle(p, lineWidth / 2.0).Contains(Position)))
                return true;

            if (this.OverlappedLinkView != null && this.OverlappedLinkView.Intersects(Position))
                return true;

            if (this.SmoothedVolumePolygon.Contains(Position))
                return true;

            //If the UI doesn't detect a hole as part of the annotation then it becomes impossible to close holes in the UI.  
            //On the other hand, a location link inside the hole is unselectable. 
            //The workaround was to assign a distance > 1 when the point falls outside the polygon.
            if (this.SmoothedVolumePolygon.InteriorPolygonContains(Position))
                return true;

            return base.Intersects(Position);
        }

        public void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Scene scene)
        {
            if (OverlappedLinkView != null)
            {
                OverlappedLinkView.DrawLabel(spriteBatch, font, scene);
            }
            curveLabels.DrawLabel(spriteBatch, font, scene);
        }

        public ICanvasView GetAnnotationAtPosition(GridVector2 position)
        {
            if (OverlappedLinkView != null)
            {
                ICanvasView containedAnnotation = OverlappedLinkView.GetAnnotationAtPosition(position);
                if (containedAnnotation != null)
                    return containedAnnotation;
            }

            if (this.Intersects(position))
                return this;

            return null;
        } 

        public override ICollection<long> OverlappedLinks
        {
            protected get
            {
                if (this.OverlappedLinkView == null)
                    return new long[0];

                return this.OverlappedLinkView.OverlappedLinks;
            }

            set
            {
                if (value == null || value.Count == 0)
                {
                    this.OverlappedLinkView = null;
                }

                this.OverlappedLinkView = new OverlappedLinkCircleView(this.InscribedCircle, this.ID, (int)this.Z, value);
                this.OverlappedLinkView.Color = this.Color;

                this.CreateLabelObjects();
            }
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {

            LocationID = this.ID;
            GridPolygon intersectingPoly; //Could be our polygon or an interior polygon

            //TODO: Detect if the View is on a touch/pen capable display
            if(Global.PenMode)
            {
                if (this.SmoothedVolumePolygon.PointIntersectsAnyPolygonSegment(WorldPosition, ControlPointRadius, out intersectingPoly))
                {
                    return LocationAction.RETRACEANDREPLACE;
                }
            }

            if(ModifierKeys.ShiftPressed())
            {
                if(this.SmoothedVolumePolygon.Contains(WorldPosition))
                {
                    return LocationAction.TRANSLATE;
                }
            }
            else if (ModifierKeys.CtrlPressed())
            { 
                //Check to see if we are on a line segment to add/remove control points.  Otherwise cut a hole
                if (this.SmoothedVolumePolygon.PointIntersectsAnyPolygonSegment(WorldPosition, ControlPointRadius, out intersectingPoly))
                {
                    if (this.VolumePolygon.PointIntersectsAnyPolygonVertex(WorldPosition, ControlPointRadius, out intersectingPoly))
                    {
                        //Cannot have a polygon with fewer than 4 verticies, We check for 4 because first and last vertex are the same.
                        if (intersectingPoly.ExteriorRing.Length > 4)
                            return LocationAction.REMOVECONTROLPOINT;
                        else
                            return LocationAction.NONE;
                    }
                    else
                        return LocationAction.ADDCONTROLPOINT;
                }
                else if(this.SmoothedVolumePolygon.Contains(WorldPosition))
                {
                    LocationID = this.ID;
                    return LocationAction.CUTHOLE;
                }
                else if(this.SmoothedVolumePolygon.InteriorPolygonContains(WorldPosition))
                {
                    LocationID = this.ID;
                    return LocationAction.REMOVEHOLE;
                }
            }
            else if (!ModifierKeys.ShiftOrCtrlPressed())
            {
                if (VisibleSectionNumber == (int)this.modelObj.Z)
                {
                    if (this.VolumePolygon.PointIntersectsAnyPolygonVertex(WorldPosition, ControlPointRadius, out intersectingPoly))
                    {
                        return LocationAction.ADJUST;
                    }
                    else if (this.SmoothedVolumePolygon.Contains(WorldPosition))
                    {
                        GridCircle TranslateTargetCircle = new GridCircle(this.InscribedCircle.Center, this.InscribedCircle.Radius / 2.0);
                        if (TranslateTargetCircle.Contains(WorldPosition))
                        {
                            LocationID = this.ID;
                            return LocationAction.TRANSLATE;
                        }

                        return LocationAction.CREATELINK;
                    }
                    else
                    {
                        return LocationAction.CREATELINKEDLOCATION;
                    }
                }
            }

            return LocationAction.NONE;
        }

        internal override void OnParentPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Label" || args.PropertyName == "Attributes")
            {
                CreateLabelObjects();
            }

            base.OnParentPropertyChanged(o, args);
        }

        internal override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            //ClearOverlappingLinkedLocationCache();

            //CreateViewObjects();
            if (IsLocationPropertyAffectingLabels(args.PropertyName))
                CreateLabelObjects();
        }

        public bool IsLabelVisible(Scene scene)
        {
            return curveLabels.IsLabelVisible(scene);
        }

        public override bool IsVisible(Scene scene)
        {
            if (Math.Min(this.BoundingBox.Width, this.BoundingBox.Height) / scene.DevicePixelWidth < 2.0)
                return false;

            return scene.VisibleWorldBounds.Intersects(this.BoundingBox);
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            if (this.SmoothedVolumePolygon.Contains(Position))
                return 0.5;
            else
                return 1.01; //This is done so we can fill interior polygons without overlapping annotations inside the polygon hole.
        }
    }
}
