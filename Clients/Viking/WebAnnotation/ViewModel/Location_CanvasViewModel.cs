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
using System.Collections.Concurrent;

namespace WebAnnotation.ViewModel
{
    public class Location_CanvasViewModel : Location_ViewModelBase
    {
        public Location_CanvasViewModel(LocationObj location)
            : base(location)
        {

        }


        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                this._AddTerminalOffEdgeMenus(menu);
                this.Parent.ContextMenu_AddUnverifiedBranchTerminals(menu);
                this._AddDeleteMenu(menu);

                return menu;
            }
        }

        public GridVector2 SectionPosition
        {
            get
            {
                return modelObj.Position;
            }
            set
            {
                modelObj.Position = value;
            }
        }

        public GridVector2 VolumePosition
        {
            get
            {
                return modelObj.VolumePosition;
            }
            set
            {
                modelObj.VolumePosition = value;
            }
        }
    }
}
