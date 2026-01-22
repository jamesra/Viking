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
using Viking.Common.UI;
using WebAnnotation.UI.Commands;
using System.Collections.Concurrent;

namespace WebAnnotation.ViewModel
{
    public class Location_CanvasViewModel(LocationObj location) : Location_ViewModelBase(location.ID)
    {
        public override ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();
                ToolStripMenuItem propertiesItem = new("Properties");
                propertiesItem.Click += ContextMenu_OnProperties;
                menu.Items.Add(propertiesItem);

                this._AddTerminalOffEdgeMenus(menu);
                this.Parent.ContextMenu_AddUnverifiedBranchTerminals(menu);
                this._AddDeleteMenu(menu);

                return menu;
            }
        }

        public GridVector2 SectionPosition => modelObj.Position;

        public GridVector2 VolumePosition => modelObj.VolumePosition;
    }
}
