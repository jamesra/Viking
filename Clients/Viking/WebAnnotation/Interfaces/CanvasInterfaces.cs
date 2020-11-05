using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using VikingXNA;
using WebAnnotation.UI;

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
    }

    public interface IPenActionSupport
    {
        LocationAction GetPenContactActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID);

        /// <summary>
        /// The user has drawn a shape that may or may not have a way to interact with this annotation.  This class tells us how to handle that shape. 
        /// </summary>   
        /// <param name="shape">Shape that was drawn</param>
        /// <param name="others">Other locations intersected by the same shape in the UI.  It may include our own shape.</param>
        /// <param name="VisibleSectionNumber">The currently viewed section</param>
        /// <returns></returns>
        List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber);
    }


    /// <summary>
    /// An interface for views that represent a Location model.
    /// </summary>
    public interface IViewLocation
    {
        /// <summary>
        /// The ID of the Location Model the view represents
        /// </summary>
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

    /// <summary>
    /// This interface is implemented by objects that require hit-testing
    /// </summary>
    public interface ICanvasView : VikingXNAGraphics.IHitTesting
    {
        /// <summary>
        /// True if the view is visible in the passed scene
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        bool IsVisible(VikingXNA.Scene scene);

        /// <summary>
        /// Indicates what level the CanvasViewObject occupies for selection and rendering purposes
        /// </summary>
        int VisualHeight { get; }

        /// <summary>
        /// True if the passed line intersects the view, a hit-testing function
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        bool Intersects(GridLineSegment line);

        /// <summary>
        /// Returns the distance from the position to the nearest point on the annotation, or 0 if the position is inside the annotation
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(GridVector2 Position);

        /// <summary>
        /// Assumes Position is within the annotation.  Returns a number from 0 to 1 indicating how close the position is between the center and edge of the annotation.
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double DistanceFromCenterNormalized(GridVector2 Position);
    }

    /// <summary>
    /// This interface is implemented by objects that require hit-testing
    /// </summary>
    public interface ICanvasGeometryView : ICanvasView
    {
        /// <summary>
        /// Distance from our view to the nearest point on the passed geometry
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(Microsoft.SqlServer.Types.SqlGeometry Position);
    }

    /// <summary>
    /// An interface for canvas views that represent any 2D shape we want to represent as a polygon
    /// </summary>
    public interface IPolygonShape
    {
        /// <summary>
        /// The 
        /// </summary>
        GridPolygon MosaicPolygon { get; }
        GridPolygon VolumePolygon { get; }
        GridPolygon SmoothedVolumePolygon { get; }
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

    /// <summary>
    /// Indicates the object can return a set of annotations that intersect geometric shapes
    /// Implemented to support hit test operations against annotations on a canvas
    /// </summary>
    public interface ICanvasViewHitTesting
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns>Annotations containing the point</returns>
        List<HitTestResult> GetAnnotations(GridVector2 WorldPosition);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <returns>Annotations intersected by the line</returns>
        List<HitTestResult> GetAnnotations(GridLineSegment line);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <returns>Annotations contained or intersected by the rectangle</returns>
        List<HitTestResult> GetAnnotations(GridRectangle rect);
    }
}
