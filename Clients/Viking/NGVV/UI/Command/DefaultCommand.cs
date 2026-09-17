using Geometry;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using Viking.Common;

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

        public virtual string[] HelpStrings => BuildHelpStrings();

        private readonly ObservableCollection<string> _ObservableHelpStrings;

        public virtual ObservableCollection<string> ObservableHelpStrings => _ObservableHelpStrings;

        private object? LastNearestObject = null;

        private string[] BuildHelpStrings()
        {
            List<string> s = [];

            if (LastNearestObject is null)
            {
                if (Parent is IHelpStrings parentHelp)
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

            return [.. s];
        }

        private string[] GetHelpStringsFromObject(object obj)
        {
            if (obj is not IHelpStrings helpStrings)
                return [];

            return helpStrings.HelpStrings;
        }

        protected object NearestObjectAtPositionAcrossAllExtensions(GridVector2 WorldPosition)
        {
            object nearest_obj = null;
            double distance = double.MaxValue;
            foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
            {
                object nearObj = overlay.ObjectAtPosition(WorldPosition, out double newDistance);
                    if (nearObj != null && newDistance < distance)
                    {
                        nearest_obj = nearObj;
                        distance = newDistance;
                    }
            }

            return nearest_obj;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            object NewLastNearestObject = NearestObjectAtPositionAcrossAllExtensions(WorldPosition);

            if (!object.Equals(NewLastNearestObject, LastNearestObject))
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

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            double distance = double.MaxValue;
            object context_obj = null;

            if (Parent.ShowOverlays)
            {
                foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                {
                    object nearObj = overlay.ObjectAtPosition(WorldPosition, out double newDistance);
                    if (nearObj != null && newDistance < distance)
                    {
                        context_obj = nearObj;
                        distance = newDistance;
                    }
                }
            }

            if (context_obj is IHandleMouseDoubleClick handler &&
                handler.HandleMouseDoubleClick(e.Button, WorldPosition))
            {
                return;
            }

            //Middle mouse button is for Wacom Pen Support
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                //Create a context menu and show it where the mouse clicked
                ContextMenuStrip menu = null;
                if (context_obj is IContextMenu menu_obj)
                {
                    menu = menu_obj.ContextMenu;
                    menu ??= new ContextMenuStrip();
                }
                else
                    menu = new ContextMenuStrip();

                IProvideContextMenus[] ContextMenuProviders = ExtensionManager.CreateContextMenuProviders();
                foreach (IProvideContextMenus provider in ContextMenuProviders)
                {
                    menu = provider.BuildMenuFor(context_obj, menu);
                }

                menu?.Show(Parent, new System.Drawing.Point(e.X, e.Y));
            }
            else
            {
                base.OnMouseDoubleClick(sender, e);
            }
        }
    }
}
