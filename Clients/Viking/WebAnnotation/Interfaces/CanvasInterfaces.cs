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

    /// <summary>
    /// This interface is implemented by objects that require hit-testing
    /// </summary>
    public interface ICanvasGeometryView : VikingXNAGraphics.ICanvasView
    {
        /// <summary>
        /// Distance from our view to the nearest point on the passed geometry
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        double Distance(Microsoft.SqlServer.Types.SqlGeometry Position);
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
        ICanvasGeometryView GetAnnotationAtPosition(GridVector2 position);
    }
}
