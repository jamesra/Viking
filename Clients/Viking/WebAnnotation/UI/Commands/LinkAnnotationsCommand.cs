using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundLineCode;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands
{
    internal class LinkAnnotationsCommand(Viking.UI.Controls.SectionViewerControl parent,
                                           LocationObj existingLoc) : AnnotationCommandBase(parent), Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        private readonly LocationObj OriginObj = existingLoc;

        /// <summary>
        /// Where the origin of the line used for rendering UI feedback is
        /// </summary>
        private readonly Geometry.Vector2 OriginPosition = GetOriginForLocation(existingLoc);
        private LocationObj? NearestTarget = null;

        /// <summary>
        /// For UI feedback this records the bounding box of the nearest target as it appears on the screen.
        /// Adjacent locations often do not display exactly where they are located so this is a quick way
        /// of tracking where the user is seeing the adjacent location rendered so we can draw a line to it.
        /// </summary>
        private Rectangle NearestTargetBoundingBox = default;

        public string[] HelpStrings => [ "Left Mouse Button Release over annotation from the same structure: Link locations to indicate morphological connection",
                                      "Left Mouse Button Release over annotation from different structure: Link structures to indicate relationship connection, for example Pre- & Post- Synaptic densities",
                                      "Escape: Cancel command"];

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        private static Geometry.Vector2 GetOriginForLocation(LocationObj obj)
        {
            return obj.TypeCode switch
            {
                LocationType.CIRCLE => obj.VolumePosition,
                LocationType.POLYGON or LocationType.CURVEPOLYGON => obj.VolumePosition,
                LocationType.OPENCURVE => Midpoint(obj.VolumeShape.ToPoints()),
                LocationType.POLYLINE => Midpoint(obj.VolumeShape.ToPoints()),
                _ => obj.VolumePosition,
            };
        }

        private static Geometry.Vector2 Midpoint(Geometry.Vector2[] array)
        {
            int i = array.Length / 2;
            return array[i];
        }

        public static IViewLocation FindBestLinkCandidate(SectionAnnotationsView sectionView, Geometry.Vector2 WorldPos, LocationObj OriginObj, out Rectangle rectBestMatchBBox)
        {
            if (sectionView is null)
            {
                throw new ArgumentNullException(nameof(sectionView));
            }

            if (OriginObj is null)
            {
                throw new ArgumentNullException(nameof(OriginObj));
            }

            rectBestMatchBBox = default;
            List<HitTestResult> listInitialHitTestResults = [.. sectionView.GetAnnotations(WorldPos).Where(ht => ht.obj != null)];
            List<HitTestResult> listHitTestResults = listInitialHitTestResults.ExpandICanvasViewContainers(WorldPos);

            //Find locations that are not equal to our origin location
            listHitTestResults = [.. listHitTestResults.Where(hr =>
            {
                if (hr.obj is not IViewLocation loc)
                {
                    return false;
                }

                return loc.ID != OriginObj.ID && !OriginObj.Links.Contains(loc.ID);
            })];

            IViewLocation nearestVisible = null;
            HitTestResult BestMatch = listHitTestResults.NearestObjectOnCurrentSectionThenAdjacent((int)OriginObj.Z);
            if (BestMatch?.obj is IViewLocation bestViewLocMatch)
            {
                nearestVisible = bestViewLocMatch;
                rectBestMatchBBox = BestMatch.obj.BoundingBox;
            }

            return nearestVisible;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <param name="candidateBoundingBox">The bounding box of the candidate we selected, used to improve UI feedback</param>
        /// <returns>The identity of the best candidate</returns>
        protected IViewLocation FindBestLinkCandidate(Geometry.Vector2 WorldPos, out Rectangle candidateBoundingBox)
        {
            candidateBoundingBox = default;
            SectionAnnotationsView sectionView = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
            return sectionView is null ? null : FindBestLinkCandidate(sectionView, WorldPos, OriginObj, out candidateBoundingBox);
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            Geometry.Vector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            IViewLocation nearestVisible = FindBestLinkCandidate(WorldPos, out Rectangle boundingBox);
            NearestTarget = TrySetTarget(nearestVisible, boundingBox);

            base.OnMouseMove(sender, e);
            Parent.Invalidate();
        }

        /// <summary>
        /// Returns the same object if it is a valid target to create a link against.  Otherwise NULL
        /// </summary>
        /// <param name="NearestTarget"></param>
        /// <returns></returns>
        private LocationObj TrySetTarget(IViewLocation nearest, in Rectangle targetBoundingRect)
        {
            if (nearest != null)
            {
                LocationObj nearest_target = Store.Locations[nearest.ID];
                LocationObj result = TrySetTarget(nearest_target);
                NearestTargetBoundingBox = targetBoundingRect;
                return nearest_target;
            }

            return null;
        }


        /// <summary>
        /// Returns the same object if it is a valid target to create a link against.  Otherwise NULL
        /// </summary>
        /// <param name="NearestTarget"></param>
        /// <returns></returns>
        private LocationObj TrySetTarget(LocationObj nearest_target)
        {
            if (nearest_target is null)
            {
                return null;
            }

            if (LocationLinkView.IsValidLocationLinkTarget(nearest_target, OriginObj))
            {
                return nearest_target;
            }

            if (StructureLinkViewModelBase.IsValidStructureLinkTarget(nearest_target, OriginObj))
            {
                return nearest_target;
            }

            return null;
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Button.Left())
            {
                Geometry.Vector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                //Find if we are close enough to a location to "snap" the line to the target
                IViewLocation nearest = FindBestLinkCandidate(WorldPos, out Rectangle boundingBox);
                NearestTarget = TrySetTarget(nearest, boundingBox);

                if (NearestTarget is null)
                {
                    Deactivated = true;
                    return;
                }

                if (LocationLinkView.IsValidLocationLinkTarget(NearestTarget, OriginObj))
                {
                    _ = CreateLocationLinkAsync();
                }
                else if (StructureLinkViewModelBase.IsValidStructureLinkTarget(NearestTarget, OriginObj))
                {
                    _ = CreateStructureLinkAsync();
                }

                Execute();
            }

            base.OnMouseUp(sender, e);
        }

        async Task CreateLocationLinkAsync()
        {
            try
            {
                await Store.LocationLinks.CreateLink(OriginObj.ID, NearestTarget.ID);
            }
            catch (Exception except)
            {
                MessageBox.Show("Could not create link between locations: " + except.Message, "Recoverable Error");
            }
        }

        async Task CreateStructureLinkAsync()
        {
            try
            {
                bool Bidirectional = NearestTarget.Parent.Type.ID == OriginObj.Parent.Type.ID;
                StructureLinkObj linkStruct = new(OriginObj.ParentID.Value, NearestTarget.ParentID.Value, Bidirectional);
                await Store.StructureLinks.Create(linkStruct);
            }
            catch (Exception except)
            {
                MessageBox.Show("Could not create link between structures: " + except.Message, "Recoverable Error");
            }
        }

        public static async Task<bool> TryCreateLink(SectionAnnotationsView sectionView, Geometry.Vector2 WorldPos, LocationObj OriginObj)
        {
            IViewLocation nearest = FindBestLinkCandidate(sectionView, WorldPos, OriginObj, out Rectangle _);
            LocationObj NearestTarget = nearest != null ? Store.Locations[nearest.ID] : null;
            if (NearestTarget is null)
            {
                return false;
            }

            if (LocationLinkView.IsValidLocationLinkTarget(NearestTarget, OriginObj))
            {
                try
                {
                    await Store.LocationLinks.CreateLink(OriginObj.ID, NearestTarget.ID);
                    return true;
                }
                catch (Exception except)
                {
                    MessageBox.Show("Could not create link between locations: " + except.Message, "Recoverable Error");
                }
            }
            else if (StructureLinkViewModelBase.IsValidStructureLinkTarget(NearestTarget, OriginObj))
            {
                try
                {
                    bool Bidirectional = NearestTarget.Parent.Type.ID == OriginObj.Parent.Type.ID;
                    StructureLinkObj linkStruct = new(OriginObj.ParentID.Value, NearestTarget.ParentID.Value, Bidirectional);
                    await Store.StructureLinks.Create(linkStruct);
                    return true;
                }
                catch (Exception except)
                {
                    MessageBox.Show("Could not create link between structures: " + except.Message, "Recoverable Error");
                }
            }

            return false;
        }

        protected override void Execute()
        {
            try
            {
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("The chosen point is outside mappable volume space, location not created", "Recoverable Error");
            }

            base.Execute();
        }

        private static readonly Color invalidTarget = new(255,
                                            0,
                                            64,
                                            0.5f);
        private static readonly Color validTarget = new(0,
                                255,
                                0,
                                128);
        private static readonly Color noTarget = new(Color.White.R,
                                    Color.White.G,
                                    Color.White.B,
                                    0.5f);
        private static readonly string? InvalidTargetStyle = null;
        private static readonly string? LocationLinkStyle = null;
        private static readonly string StructureLinkStyle = "AnimatedLinear";

        private double LineRadiusForLocationLink() => OriginObj.Radius / 6.0;
        private double LineRadiusForStructureLink()
        {
            if (NearestTarget is null)
            {
                return OriginObj.Radius;
            }

            return Math.Min(OriginObj.Radius, NearestTarget.Radius);
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (oldMouse is null)
            {
                return;
            }

            Vector3 target;
            if (NearestTarget != null)
            {
                //Snap the line to a nearby target if it exists
                Geometry.Vector2 targetPos = NearestTargetBoundingBox.Center; //GetOriginForLocation(NearestTarget);

                target = new Vector3((float)targetPos.X, (float)targetPos.Y, 0f);
            }
            else
            {
                //Otherwise use the old mouse position
                target = new Vector3((float)oldWorldPosition.X, (float)oldWorldPosition.Y, 0f);
            }

            Color lineColor = noTarget;
            string lineStyle = null;
            double lineRadius = LineRadiusForLocationLink();
            bool UseLumaLineManager = false;

            if (NearestTarget != null)
            {
                if (LocationLinkView.IsValidLocationLinkTarget(NearestTarget, OriginObj))
                {
                    lineColor = validTarget;
                    lineStyle = LocationLinkStyle;
                    lineRadius = LineRadiusForLocationLink();
                    UseLumaLineManager = true;
                }
                else if (StructureLinkViewModelBase.IsValidStructureLinkTarget(NearestTarget, OriginObj))
                {
                    lineColor = validTarget;
                    lineStyle = StructureLinkStyle;
                    lineRadius = LineRadiusForStructureLink();
                    UseLumaLineManager = false;
                }
                else
                {
                    lineColor = invalidTarget;
                    lineStyle = InvalidTargetStyle;
                    lineRadius = LineRadiusForLocationLink();
                    UseLumaLineManager = true;
                }
            }

            RoundLine lineToParent = new((float)OriginPosition.X,
                                                   (float)OriginPosition.Y,
                                                   target.X,
                                                   target.Y);

            float Time = (float)TimeSpan.FromTicks(DateTime.Now.Ticks - DateTime.Today.Ticks).TotalSeconds;
            RoundLineManager lineManager = UseLumaLineManager ? Parent.LumaOverlayLineManager : Parent.LineManager;
            lineColor = UseLumaLineManager ? lineColor.ConvertToHCL() : lineColor;
            lineManager.Draw(lineToParent,
                                    (float)(lineRadius),
                                    lineColor,
                                    basicEffect.View * basicEffect.Projection,
                                    Time,
                                    lineStyle);


            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
