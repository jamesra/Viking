using System;
using System.ComponentModel; 
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; 
using Viking.Common;
using WebAnnotation;
using WebAnnotationModel;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using Common.UI;
using WebAnnotation.UI.Commands; 

namespace WebAnnotation.ViewModel
{
    public class OverlappedLocation : Location_CanvasViewModel
    {
        public readonly LocationLink link;
        public readonly GridCircle gridCircle;

        public OverlappedLocation(LocationLink linkObj, LocationObj location, GridCircle circle) : base(location)
        {
            link = linkObj; 
            gridCircle = circle; 
        }

        public override ContextMenu ContextMenu
        {
            get
            {
                return link.ContextMenu;
            }
        }
    }
}
