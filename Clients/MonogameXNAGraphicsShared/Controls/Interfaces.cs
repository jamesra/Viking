using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace VikingXNAGraphics.Controls
{
    public delegate void OnClickEventHandler(object sender, VikingXNAGraphics.Controls.MouseButton button);

    //public interface IActionCommandFactory
    public interface IClickable
    {
        /// <summary>
        /// Called by the owner window or control.  Implementation should call the Button's OnClick
        /// Event if the point is within the button.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="button"></param>
        /// <returns>True if the button contained the point and an event was fired</returns>
        bool TryMouseClick(GridVector2 point, MouseButton button);

        /// <summary>
        /// Called with the object being clicked as IMouseClickable and any input device state as an object
        /// </summary>
        event Action<IClickable, object> OnClick;

        /// <summary>
        /// Called by the owner window or control.  Implementation should call the button's OnClick
        /// Event if the poitn is within the button.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="penInfo"></param>
        /// <returns></returns>
        bool TryPenClick(GridVector2 point, object penInfo);
    }
}
