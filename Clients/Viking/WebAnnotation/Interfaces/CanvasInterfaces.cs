using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Microsoft.Xna.Framework.Graphics;
using Geometry;
using WebAnnotation.View;

namespace WebAnnotation
{
    public interface ISelectable
    {
        bool Selected { get; set; }
    }

    public interface ILabelView
    {
        void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.Scene scene);

        bool IsLabelVisible(Scene scene);
    }

    public interface IRenderedLabelView
    {
        void DrawLabel(GraphicsDevice device, SpriteBatch spriteBatch, SpriteFont font, VikingXNA.Scene scene);

        bool IsLabelVisible(Scene scene);
    }

    public interface IMouseActionSupport
    {
        /// <summary>
        /// Return the action and LocationID for a mouse click at a given position
        /// </summary>
        /// <param name="WorldPosition">Where the mouse is</param>
        /// <param name="VisibleSectionNumber">Which section is being viewed</param>
        /// <param name="LocationID">The location ID the action applies to</param>
        /// <returns></returns>
        LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID);

        /*
        LocationAction GetMouseShiftClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber);

        LocationAction GetMouseControlClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber);
        */
    }

    //public interface IActionCommandFactory

    
    public interface IViewLocation
    {
        long ID { get; }
    }

    public interface IViewLocationLink
    {
        WebAnnotationModel.LocationLinkKey Key { get; }
    }

    public interface IViewStructure
    {
        long ID { get; }
    }

    public interface IViewStructureLink
    {
        WebAnnotationModel.StructureLinkKey Key { get; }
    }

    public interface IViewStructureType
    {
        long ID { get; }
    }

    public interface ICanvasView
    {
        bool IsVisible(Scene scene);

        /// <summary>
        /// Bounding box of the annotation
        /// </summary>
        GridRectangle BoundingBox
        {
            get;
        }

        bool Intersects(GridVector2 Position);

        /// <summary>
        /// Returns the distance from the position to the nearest point on the annotation, or 0 if the position is inside the annotation
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(GridVector2 Position);


        double Distance(Microsoft.SqlServer.Types.SqlGeometry Position);

        /// <summary>
        /// Assumes Position is within the annotation.  Returns a number from 0 to 1 indicating how close the position is between the center and edge of the annotation.
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double DistanceFromCenterNormalized(GridVector2 Position);
    }

    /// <summary>
    /// A class that contains multiple ICanvasView objects
    /// </summary>
    public interface ICanvasViewContainer
    {
        /// <summary>
        /// Some annotations can nest annotations inside, such as the stylized mini-circles for overlapped locations that are embedded in circle annotations.
        /// This function returns which annotation the mouse is over, or the parent if the mouse is not over a nested annotation.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="distanceToCenterNormalized"></param>
        /// <returns></returns>
        ICanvasView GetAnnotationAtPosition(GridVector2 position);
    }
}
