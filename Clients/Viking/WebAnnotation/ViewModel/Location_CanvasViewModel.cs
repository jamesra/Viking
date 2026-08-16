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
using WebAnnotationModel.Objects;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using Viking.Common.UI;
using WebAnnotation.UI.Commands;
using System.Collections.Concurrent;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

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

        public Geometry.Vector2 SectionPosition => modelObj.Position;

        public Geometry.Vector2 VolumePosition => modelObj.VolumePosition;
    }
}
