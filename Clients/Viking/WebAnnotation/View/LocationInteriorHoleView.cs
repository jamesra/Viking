using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System.Collections.Generic;
using System.Windows.Forms;
using VikingXNA;
using WebAnnotation.UI;

namespace WebAnnotation.View
{
    /// <summary>
    /// Represents a hole in an annotation. 
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="LocationID"></param>
    /// <param name="innerPoly"></param>
    /// <param name="volumePolygon">The interior polygon</param>
    /// <param name="smoothVolumePolygon">The smoothed interior polygon</param>
    internal class LocationInteriorHoleView(long LocationID, int innerPoly, Polygon volumePolygon, Polygon smoothVolumePolygon) : ICanvasGeometryView, Viking.Common.IHelpStrings, Viking.Common.IContextMenu,
                                       IMouseActionSupport, IPenActionSupport
    {
        private readonly Polygon VolumePolygon = volumePolygon;
        private readonly Polygon SmoothedVolumePolygon = smoothVolumePolygon;
        private readonly SqlGeometry VolumeShapeAsRendered = smoothVolumePolygon.ToSqlGeometry();

        /// <summary>
        /// Identity of the Location with the interior hole
        /// </summary>
        private readonly long ID = LocationID;

        /// <summary>
        /// Index of the inner polygon this view represents
        /// </summary>
        public readonly int iInnerPolygon = innerPoly;

        public int VisualHeight => 0;

        public Rectangle BoundingBox => SmoothedVolumePolygon.BoundingBox;

        public string[] HelpStrings
        {
            get
            {
                List<string> listStrings = [];
                if (Global.PenMode)
                {
                    listStrings.Add("Draw path across shape: Replace annotation boundary");
                }

                listStrings.Add("CTRL + Left click on interior hole: Remove interior hole");
                listStrings.Add("CTRL + Left click inside shape: Cut hole in annotation");

                return [.. listStrings];
            }
        }

        public ContextMenuStrip ContextMenu
        {
            get
            {
                ViewModel.Location_ViewModelBase view_model = new(ID);
                ContextMenuStrip menu = new();
                ToolStripMenuItem simplify_item = new("Simplify Polygon")
                {
                    Tag = new int?(iInnerPolygon)
                };
                simplify_item.Click += view_model.ContextMenu_SimplifyPolygon;
                menu.Items.Add(simplify_item);

                ToolStripMenuItem remove_inner_poly = new("Remove interior hole")
                {
                    Tag = new int?(iInnerPolygon)
                };
                remove_inner_poly.Click += view_model.ContextMenu_RemoveInnerPolygon;
                menu.Items.Add(remove_inner_poly);
                return menu;
            }
        }
        public bool Contains(Vector2 Position) => SmoothedVolumePolygon.Covers(Position);

        public double Distance(SqlGeometry Shape) => VolumeShapeAsRendered.STDistance(Shape).Value;

        public double Distance(Vector2 Position) => SmoothedVolumePolygon.Distance(Position);

        public double DistanceFromCenterNormalized(Vector2 Position) => SmoothedVolumePolygon.Distance(Position);

        public bool Intersects(LineSegment line) => SmoothedVolumePolygon.Intersects(line);

        public bool IsVisible(Scene scene) => true;

        public LocationAction GetMouseClickActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (ModifierKeys.CtrlPressed())
            {
                return LocationAction.REMOVEHOLE;
            }

            return LocationAction.NONE;
        }

        public LocationAction GetPenContactActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Keys ModifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (ModifierKeys.CtrlPressed())
            {
                return LocationAction.REMOVEHOLE;
            }

            return LocationAction.NONE;
        }

        public List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) =>
            //TODO: We might be able to optimize by moving interior hole action checks here
            [];
    }
}