using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Geometry;
using WebAnnotation.ViewModel;

namespace WebAnnotation.View
{
    public enum LocationAction
    {
        NONE,
        TRANSLATE, //Move the annotation
        SCALE,     //Scale the size of the annotation
        ADJUST,    //Adjust a specific control point
        CREATELINK //Create a link to an adjacent location or structure
    }

    /// <summary>
    /// Manage actions available to locations
    /// </summary>
    static class LocationActions
    {
        public static Cursor GetCursor(this LocationAction action)
        {
            switch(action)
            {
                case LocationAction.NONE:
                    return Cursors.Default;
                case LocationAction.TRANSLATE:
                    return Cursors.Hand;
                case LocationAction.SCALE:
                    return Cursors.SizeAll;
                case LocationAction.ADJUST:
                    return Cursors.Hand;
                case LocationAction.CREATELINK:
                    return Cursors.Cross;
                default:
                    return Cursors.Default;
            }
        }
    }
}
