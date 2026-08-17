using Geometry;
using Viking.Input;
using Microsoft.SqlServer.Types;
using System.Collections.Generic;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif
using VikingXNA;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;

namespace WebAnnotation.View
{
    /// <summary>
    /// Interior hole of a polygon. Click is REMOVEHOLE. Contains() is true inside the hole so the parent can still offer that action.
    /// </summary>
    /// <param name="LocationID"></param>
    /// <param name="innerPoly"></param>
    /// <param name="volumePolygon">The interior polygon</param>
    /// <param name="smoothVolumePolygon">The smoothed interior polygon</param>
    internal class LocationInteriorHoleView(long LocationID, int innerPoly, Polygon volumePolygon, Polygon smoothVolumePolygon) : ICanvasGeometryView, Viking.Common.IHelpStrings,
#if NETFRAMEWORK
        Viking.Common.IContextMenu,
#endif
        IMouseActionSupport, IPenActionSupport
    {
        private readonly Polygon VolumePolygon = volumePolygon;
        private readonly Polygon SmoothedVolumePolygon = smoothVolumePolygon;
        private readonly SqlGeometry VolumeShapeAsRendered;

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

#if NETFRAMEWORK
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
#endif
        public bool Contains(Vector2 Position) => SmoothedVolumePolygon.Covers(Position);

        public double Distance(SqlGeometry Shape) => VolumeShapeAsRendered.STDistance(Shape).Value;

        public double Distance(Vector2 Position) => SmoothedVolumePolygon.Distance(Position);

        public double DistanceFromCenterNormalized(Vector2 Position) => SmoothedVolumePolygon.Distance(Position);

        public bool Intersects(LineSegment line) => SmoothedVolumePolygon.Intersects(line);

        public bool IsVisible(Scene scene) => true;

        public LocationAction GetMouseClickActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.CtrlPressed())
            {
                return LocationAction.REMOVEHOLE;
            }

            return LocationAction.NONE;
        }

        public LocationAction GetPenContactActionForPositionOnAnnotation(Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.CtrlPressed())
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