using Viking.AnnotationServiceTypes;
using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using VikingXNA;
using WebAnnotation.UI;
using WebAnnotationModel;
using LocationLinkKey = Viking.AnnotationServiceTypes.LocationLinkKey;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

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


    /// <summary>Mouse click maps to one LocationAction. Pen uses IPenActionSupport, which can return several IAction items.</summary>
    public interface IMouseActionSupport
    {
        /// <summary>
        /// Return the action and LocationID for a mouse click at a given position
        /// </summary>
        /// <param name="WorldPosition">Where the mouse is</param>
        /// <param name="VisibleSectionNumber">Which section is being viewed</param>
        /// <param name="LocationID">The location ID the action applies to</param>
        /// <returns></returns>
        LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID);
    }

    public interface IPenActionSupport
    {
        LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID);

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
        LocationLinkKey Key { get; }
    }

    public interface IViewStructure
    {
        long ID { get; }
    }

    public interface IViewStructureLink
    {
        StructureLinkKey Key { get; }
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
        /// Tie-break for overlapping hits. Locations use structure nesting (ParentDepth), not Z. Links use 0 so locations win.
        /// </summary>
        int VisualHeight { get; }

        /// <summary>
        /// True if the passed line intersects the view, a hit-testing function
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        bool Intersects(LineSegment line);

        /// <summary>
        /// Returns the distance from the position to the nearest point on the annotation, or 0 if the position is inside the annotation
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(Geometry.Vector2 Position);

        /// <summary>
        /// 0 at center, 1 at the edge. Values above 1 mean the point is not truly inside (holes use 1.01 so REMOVEHOLE still hits but selection prefers the interior annotation).
        /// </summary>
        double DistanceFromCenterNormalized(Geometry.Vector2 Position);
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
    /// Mosaic is section-space source. Volume is stos-mapped. SmoothedVolume is the drawn outline — hit-test against that, not Mosaic.
    /// </summary>
    public interface IPolygonShape
    {
        Polygon MosaicPolygon { get; }
        Polygon VolumePolygon { get; }
        Polygon SmoothedVolumePolygon { get; }
    }



    /// <summary>
    /// A class that contains multiple ICanvasView objects
    /// </summary>
    public interface ICanvasViewContainer
    {
        /// <summary>
        /// Nested overlap arrows/circles win over the parent when the pointer is on a child. LocationID from the child may not be the parent's ID.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="distanceToCenterNormalized"></param>
        /// <returns></returns>
        ICanvasView GetAnnotationAtPosition(Geometry.Vector2 position);
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
        List<HitTestResult> GetAnnotations(Geometry.Vector2 WorldPosition);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <returns>Annotations intersected by the line</returns>
        List<HitTestResult> GetAnnotations(LineSegment line);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <returns>Annotations contained or intersected by the rectangle</returns>
        List<HitTestResult> GetAnnotations(Rectangle rect);
    }
}
