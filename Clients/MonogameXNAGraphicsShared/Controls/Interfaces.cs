using Geometry;
using System;

namespace VikingXNAGraphics.Controls
{
    public enum InputDevice
    {
        Mouse,
        Pen
    }
     
    //public delegate void OnClickEventHandler(object sender, VikingXNAGraphics.Controls.MouseButton button);

    /// <summary>
    /// Called by an owner window or control when the IHitTesting.Contains implementation is true for the point clicked.
    /// </summary>
    /// <param name="point">Point device was clicked at in world coordinates</param>
    /// <param name="source">Type of Device</param>
    /// <param name="source_info">Input device information (ie: mouse button info or pen info) </param>
    /// <returns>True if the input event should be considered handled.</returns>
    public delegate bool InputDeviceEventConsumerDelegate(IClickable sender, GridVector2 point, InputDevice source, object source_info);

    /// <summary>
    /// Called by an owner window or control when the input device has sent an event that is relevant to the implementation.
    /// This event is passive in that it does not consume the input events and stop other listeners from consuming them. 
    /// For example, OnMouseEnter
    /// </summary>
    /// <param name="point">Point device was clicked at in world coordinates</param>
    /// <param name="source">Type of Device</param>
    /// <param name="source_info">Input device information (ie: mouse button info or pen info) </param>
    public delegate void InputDeviceEventPassiveDelegate(IClickable sender, GridVector2 point, InputDevice source, object source_info);

    /// <summary>
    /// This interface has Contains called on it by the owner.  If it contains a click
    /// then the owner should call OnMouseClick or OnPenClick appropriately
    /// </summary>
    public interface IClickable : IHitTesting
    {
        /// <summary>
        /// Called by the owner window or control when the IHitTesting.Contains implementation is true for the point clicked.
        /// </summary>
        /// <returns>True if the button contained the point and an event was fired</returns>
        InputDeviceEventConsumerDelegate OnClick { get; } 
    }

    /// <summary>
    /// This interface has Contains called on it by the owner.  If it contains a click
    /// then the owner should call OnMouseClick or OnPenClick appropriately
    /// </summary>
    public interface IHoverable : IHitTesting
    {
        /// <summary>
        /// Called by the owner window or control when the IHitTesting.Contains implementation transitions from false to true
        /// </summary>
        /// <returns></returns>
        InputDeviceEventPassiveDelegate OnEnter { get; }

        /// <summary>
        /// Called by the owner window or control when the IHitTesting.Contains implementation transitions from false to true
        /// </summary>
        /// <returns></returns>
        InputDeviceEventPassiveDelegate OnLeave { get; }
    }

    /// <summary>
    /// A helper class that takes a geometry as input and can have 
    /// an arbitrary OnClick implementation assigned at construction.
    /// Useful to pair with a view to isolate views from UI actions
    /// </summary>
    public class ClickableGeometryWrapper : IClickable
    {
        public IShape2D Shape;

        public static ClickableGeometryWrapper CreateSimple(IShape2D shape, Action action)
        {
            ClickableGeometryWrapper obj = new ClickableGeometryWrapper(shape);
            obj.OnClick = new InputDeviceEventConsumerDelegate((sender, position, input_source, input_data) => { action(); return true; });
            return obj;
        }

        public ClickableGeometryWrapper(IShape2D shape)
        {
            if (shape == null)
                throw new ArgumentNullException("IShape2D being wrapped cannot be null");
            Shape = shape;
        }

        public InputDeviceEventConsumerDelegate OnClick { get; set; }

        public GridRectangle BoundingBox => Shape.BoundingBox;
        
        public bool Contains(GridVector2 Position)
        {
            return Shape.Contains(Position);
        }
    }
}
