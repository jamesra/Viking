using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using Geometry; 

namespace Viking.UI.Commands
{

    /// <summary>
    /// The default command allows scrolling around the view and selecting existing items
    /// </summary>
    [Viking.Common.CommandAttribute()]
    public class DefaultCommand : Command
    {
        public DefaultCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {

        }

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
                double distance = double.MaxValue;
                IContextMenu context_obj = null;

                if (Parent.ShowOverlays)
                {
                    foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                    {

                        double newDistance;
                        object nearObj = overlay.ObjectAtPosition(WorldPosition, out newDistance);
                        if (nearObj != null)
                        {
                            if (newDistance < distance)
                            {
                                context_obj = nearObj as IContextMenu;
                                distance = newDistance;
                            }
                        }
                    }
                }

                //Create a context menu and show it where the mouse clicked
                //Right mouse button calls up context menu
                ContextMenu menu = null; 
                if (context_obj != null)
                    menu = context_obj.ContextMenu;
                else
                    menu = new ContextMenu();
                
                //Talk to everyone who modifies context menus to see if they have a contribution
                IProvideContextMenus[] ContextMenuProviders = ExtensionManager.CreateContextMenuProviders();
                foreach (IProvideContextMenus provider in ContextMenuProviders)
                {
                    menu = provider.BuildMenuFor(context_obj, menu);
                }

                if (menu != null)
                {
                    menu.Show(Parent, new System.Drawing.Point(e.X, e.Y));
                }
            }
            else
            {
                base.OnMouseDoubleClick(sender, e);
            }
        }
    }
}
