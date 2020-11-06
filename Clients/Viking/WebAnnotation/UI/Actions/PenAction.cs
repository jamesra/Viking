using System;
using VikingXNAGraphics;

namespace WebAnnotation
{
    /// <summary>
    /// Represents an action that can occur for an annotation
    /// </summary>
    public interface IAction : IEquatable<IAction>
    {
        LocationAction Type { get; }

        /// <summary>
        /// A delegate to a function that will perform the action
        /// </summary>
        Action Execute { get; }
    }

    /// <summary>
    /// IActionView is implemented by Actions who want to provide user feedback for the action
    /// </summary>
    public interface IActionView
    {
        /// <summary>
        /// An always active visualization
        /// </summary>
        IRenderable Passive { get; set; }

        /// <summary>
        /// A more descriptive visualization that can be turned on.  
        /// For example when the mouse is over a button
        /// </summary>
        IRenderable Active { get; set; }
    }



    /// <summary>
    /// Binds an action to a clickable view
    /// </summary>
    public class ViewActionBinding
    {
        VikingXNAGraphics.Controls.IClickable ClickableAction;

        IActionView View;
    }


    /// <summary>
    /// Describes an action a user can confirm after drawing a shape with a pen that has zero, one, or more possible meanings
    /// </summary>
    public abstract class AnnotationAction : IAction, IActionView
    {
        /// <summary>
        /// The general class of action this instance will perform
        /// </summary>
        public abstract LocationAction Type { get; }

        public abstract BuiltinTexture Icon { get; set; }

        public Action Execute => OnExecute;

        public static implicit operator Action(AnnotationAction a) => a.Execute;

        /// <summary>
        /// An always active visualization
        /// </summary>
        public abstract IRenderable Passive { get; set; }

        /// <summary>
        /// A more descriptive visualization that can be turned on.  
        /// For example when the mouse is over a button
        /// </summary>
        public abstract IRenderable Active { get; set; }

        public abstract void OnExecute();

        public abstract bool Equals(IAction other);
    }
    /*
    static class PenActionExtension
    {
        /// <summary>
        /// Given a set of input state determine if a hole could be cut in the annotation
        /// </summary>
        /// <param name="loc"></param>
        /// <param name="new_hole"></param>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static CutHoleAction TryCreateCutHole(LocationObj loc, GridPolygon new_hole, IVolumeToSectionTransform transform)
        {
            Action action = new Action(() =>
            {
                GridVector2[] mosaic_points = transform.VolumeToSection(new_hole.ExteriorRing);
                SqlGeometry updatedMosaicShape = loc.MosaicShape.AddInteriorPolygon(mosaic_points);

                try
                {
                    loc.SetShapeFromGeometryInSection(transform, updatedMosaicShape);
                }
                catch (ArgumentException e)
                {
                    MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                Store.Locations.Save();
            }

            CutHoleAction output = new 


        }
    }*/

}
