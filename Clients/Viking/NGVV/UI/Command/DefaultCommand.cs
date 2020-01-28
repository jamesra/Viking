using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class DefaultCommand : Command, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        public DefaultCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            _ObservableHelpStrings = new ObservableCollection<string>(this.HelpStrings);
        }

        public virtual string[] HelpStrings
        {
            get
            {
                return BuildHelpStrings();
            }
        }

        private ObservableCollection<string> _ObservableHelpStrings;

        public virtual ObservableCollection<string> ObservableHelpStrings { get { return _ObservableHelpStrings; } }

        private object LastNearestObject = null;

        private string[] BuildHelpStrings()
        {
            List<string> s = new List<string>();

            if (LastNearestObject == null)
            {
                IHelpStrings parentHelp = Parent as IHelpStrings;
                if(parentHelp != null)
                {
                    s.AddRange(parentHelp.HelpStrings);
                }

                s.AddRange(Command.DefaultKeyHelpStrings);
                s.AddRange(Command.DefaultMouseHelpStrings);
                s.Add("Double Right Click: Open context menu for annotation");

                if (ExtensionManager.SectionOverlays != null)
                {
                    foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                    {
                        s.AddRange(GetHelpStringsFromObject(overlay));
                    }
                }
            }
            else
            {
                s.AddRange(GetHelpStringsFromObject(LastNearestObject));
            }

            s.Sort();

            return s.ToArray();
        }

        private string[] GetHelpStringsFromObject(object obj)
        {
            IHelpStrings helpStrings = obj as IHelpStrings;
            if (helpStrings == null)
                return new string[] { };

            return helpStrings.HelpStrings;
        }

        protected object NearestObjectAtPositionAcrossAllExtensions(GridVector2 WorldPosition)
        {
            object nearest_obj = null;
            double distance = double.MaxValue;
            foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
            {
                double newDistance;
                object nearObj = overlay.ObjectAtPosition(WorldPosition, out newDistance);
                if (nearObj != null)
                {
                    if (newDistance < distance)
                    {
                        nearest_obj = nearObj as IContextMenu;
                        distance = newDistance;
                    }
                }
            }

            return nearest_obj;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            object NewLastNearestObject = NearestObjectAtPositionAcrossAllExtensions(WorldPosition);

            if(!object.Equals(NewLastNearestObject, LastNearestObject))
            {
                LastNearestObject = NewLastNearestObject;

                ObservableHelpStrings.Clear();
                foreach (string helpStr in this.HelpStrings)
                {
                    ObservableHelpStrings.Add(helpStr);
                }
            } 
             
            base.OnMouseMove(sender, e);
        }

        protected virtual void OnPenMove(object sender, PenEventArgs e)
        {
            
            base.OnPenMove(sender, e);
            return;
        }

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            //Middle mouse button is for Wacom Pen Support
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
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
