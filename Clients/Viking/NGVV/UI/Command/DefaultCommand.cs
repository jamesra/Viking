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

        /// <summary>
        /// Idle F1 catalog. Order is viewer keys, then overlay keys, then either the annotation
        /// under the cursor or the idle command lines. Lines are not sorted, so a sequence of steps stays in order.
        /// </summary>
        private string[] BuildHelpStrings()
        {
            List<string> s = [];

            if (Parent is IHelpStrings parentHelp)
            {
                s.AddRange(parentHelp.HelpStrings);
            }

            if (ExtensionManager.SectionOverlays != null)
            {
                foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                {
                    s.AddRange(GetHelpStringsFromObject(overlay));
                }
            }

            if (LastNearestObject is null)
            {
                s.AddRange(Command.DefaultKeyHelpStrings);
                s.AddRange(Command.DefaultMouseHelpStrings);
                s.Add("Double Right Click: Open context menu for annotation");
            }
            else
            {
                s.AddRange(GetHelpStringsFromObject(LastNearestObject));
            }

            return [.. s];
        }

        private string[] GetHelpStringsFromObject(object obj)
        {
            if (obj is not IHelpStrings helpStrings)
                return [];

            return helpStrings.HelpStrings;
        }

        protected object NearestObjectAtPositionAcrossAllExtensions(Vector2 WorldPosition)
        {
            object nearest_obj = null;
            double distance = double.MaxValue;
            foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
            {
                object nearObj = overlay.ObjectAtPosition(WorldPosition, out double newDistance);
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

        /// <summary>
        /// Rebuilds the idle catalog when the annotation under the cursor changes.
        /// Mouse and pen both call this so a pen over a shape updates the F1 bar.
        /// </summary>
        private void RefreshHelpForWorldPosition(Vector2 worldPosition)
        {
            object nearest = NearestObjectAtPositionAcrossAllExtensions(worldPosition);
            if (object.Equals(nearest, LastNearestObject))
                return;

            LastNearestObject = nearest;
            ObservableHelpStrings.Clear();
            foreach (string helpStr in HelpStrings)
            {
                ObservableHelpStrings.Add(helpStr);
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            RefreshHelpForWorldPosition(Parent.ScreenToWorld(e.X, e.Y));
            base.OnMouseMove(sender, e);
        }

        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            RefreshHelpForWorldPosition(Parent.ScreenToWorld(e.X, e.Y));
            base.OnPenMove(sender, e);
        }

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            //Middle mouse button is for Wacom Pen Support
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
                double distance = double.MaxValue;
                object context_obj = null;

                if (Parent.ShowOverlays)
                {
                    foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                    {
                        object nearObj = overlay.ObjectAtPosition(WorldPosition, out double newDistance);
                        if (nearObj != null)
                        {
                            if (newDistance < distance)
                            {
                                context_obj = nearObj;
                                distance = newDistance;
                            }
                        }
                    }
                }

                //Create a context menu and show it where the mouse clicked
                //Right mouse button calls up context menu
                ContextMenuStrip menu = null;
                if (context_obj != null)
                    if (context_obj is IContextMenu menu_obj)
                    {
                        menu = menu_obj.ContextMenu;
                        menu ??= new ContextMenuStrip();
                    }
                    else
                        menu = new ContextMenuStrip();
                else
                    menu = new ContextMenuStrip();

                //Talk to everyone who modifies context menus to see if they have a contribution
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
