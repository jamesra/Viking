using Geometry;
using Microsoft.SqlServer.Types;
using System.Collections.Generic;
using System.Windows.Forms;
using VikingXNA;
using WebAnnotation.UI;

namespace WebAnnotation.View
{
    /// <summary>
    /// Represents a hole in an annotation. 
    /// </summary>
    internal class LocationInteriorHoleView : ICanvasGeometryView, Viking.Common.IHelpStrings, Viking.Common.IContextMenu,
                                       IMouseActionSupport, IPenActionSupport
    {
        private readonly GridPolygon VolumePolygon;
        private readonly GridPolygon SmoothedVolumePolygon;
        private readonly SqlGeometry VolumeShapeAsRendered;

        /// <summary>
        /// Identity of the Location with the interior hole
        /// </summary>
        private readonly long ID;

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

        public ContextMenu ContextMenu
        {
            get
            {
                ViewModel.Location_ViewModelBase view_model = new WebAnnotation.ViewModel.Location_ViewModelBase(ID);
                ContextMenu menu = new ContextMenu();
                MenuItem simplify_item = new MenuItem("Simplify Polygon", view_model.ContextMenu_SimplifyPolygon)
                {
                    Tag = new int?(iInnerPolygon)
                };
                menu.MenuItems.Add(simplify_item);

                MenuItem remove_inner_poly = new MenuItem("Remove interior hole", view_model.ContextMenu_RemoveInnerPolygon)
                {
                    Tag = new int?(iInnerPolygon)
                };
                menu.MenuItems.Add(remove_inner_poly);
                return menu;
            }
        }
        public bool Contains(GridVector2 Position)
        {
            return SmoothedVolumePolygon.Contains(Position);
        }

        public double Distance(SqlGeometry Shape)
        {
            return VolumeShapeAsRendered.STDistance(Shape).Value;
        }

        public double Distance(GridVector2 Position)
        {
            return SmoothedVolumePolygon.Distance(Position);
        }

        public double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return SmoothedVolumePolygon.Distance(Position);
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
            LocationID = ID;

            if (ModifierKeys.CtrlPressed())
            {
                return LocationAction.REMOVEHOLE;
            }

            return LocationAction.NONE;
        }

        public LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
        {
            LocationID = ID;

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