using Geometry;
using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using VikingXNA;
using WebAnnotation.UI;

namespace WebAnnotation.View
{
    /// <summary>
    /// Represents a hole in an annotation. 
    /// </summary>
    class LocationInteriorHoleView : ICanvasGeometryView, Viking.Common.IHelpStrings, Viking.Common.IContextMenu,
                                       IMouseActionSupport, IPenActionSupport
    {
        GridPolygon VolumePolygon;
        GridPolygon SmoothedVolumePolygon;

        SqlGeometry VolumeShapeAsRendered;

        /// <summary>
        /// Identity of the Location with the interior hole
        /// </summary>
        readonly long ID;

        /// <summary>
        /// Index of the inner polygon this view represents
        /// </summary>
        public readonly int iInnerPolygon;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="LocationID"></param>
        /// <param name="innerPoly"></param>
        /// <param name="volumePolygon">The interior polygon</param>
        /// <param name="smoothVolumePolygon">The smoothed interior polygon</param>
        public LocationInteriorHoleView(long LocationID, int innerPoly, GridPolygon volumePolygon, GridPolygon smoothVolumePolygon)
        {
            ID = LocationID;
            iInnerPolygon = innerPoly;

            VolumePolygon = volumePolygon;
            SmoothedVolumePolygon = smoothVolumePolygon;
        }

        public int VisualHeight => 0;

        public GridRectangle BoundingBox => SmoothedVolumePolygon.BoundingBox;

        public string[] HelpStrings
        {
            get
            {
                List<string> listStrings = new List<string>();
                if (Global.PenMode)
                {
                    listStrings.Add("Draw path across shape: Replace annotation boundary");
                }

                listStrings.Add("CTRL + Left click on interior hole: Remove interior hole");
                listStrings.Add("CTRL + Left click inside shape: Cut hole in annotation");

                return listStrings.ToArray();
            }
        }

        public ContextMenu ContextMenu => throw new NotImplementedException();

        public bool Contains(GridVector2 Position)
        {
            return SmoothedVolumePolygon.Contains(Position);
        }

        public double Distance(SqlGeometry Shape)
        {
            return this.VolumeShapeAsRendered.STDistance(Shape).Value;
        }

        public double Distance(GridVector2 Position)
        {
            return this.SmoothedVolumePolygon.Distance(Position);
        }

        public double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return this.SmoothedVolumePolygon.Distance(Position);
        }

        public bool Intersects(GridLineSegment line)
        {
            return SmoothedVolumePolygon.Intersects(line);
        }

        public bool IsVisible(Scene scene)
        {
            return true;
        }

        public LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;

            if (ModifierKeys.CtrlPressed())
            {
                return LocationAction.REMOVEHOLE;
            }

            return LocationAction.NONE;
        }

        public LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;

            if (ModifierKeys.CtrlPressed())
            {
                return LocationAction.REMOVEHOLE;
            }

            return LocationAction.NONE;
        }

        public List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            //TODO: We might be able to optimize by moving interior hole action checks here
            return new List<IAction>();
        }
    }
}