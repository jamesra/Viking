using Geometry;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.Commands
{

    /// <summary>
    /// The default command allows scrolling around the view and selecting existing items
    /// </summary>
    [Viking.Common.CommandAttribute()]
    public class DefaultCommand : Command, Viking.Common.IHelp, Viking.Common.IObservableHelp
    {
        public DefaultCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            _ObservableHelp = new ObservableCollection<HelpEntry>(this.Help);
        }

        public virtual HelpEntry[] Help => BuildHelp();

        private readonly ObservableCollection<HelpEntry> _ObservableHelp;

        public virtual ObservableCollection<HelpEntry> ObservableHelp => _ObservableHelp;

        private object? LastNearestObject = null;

        /// <summary>
        /// Idle F1 catalog: the viewer's keys, the overlays' keys, then either the annotation under the cursor or the idle
        /// command's keys and mouse actions. The help bar arranges it (plain-text lines in this order, then keys sorted).
        /// </summary>
        private HelpEntry[] BuildHelp()
        {
            List<HelpEntry> s = [];

            if (Parent is IHelp parentHelp)
            {
                s.AddRange(parentHelp.Help);
            }

            if (ExtensionManager.SectionOverlays != null)
            {
                foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                {
                    s.AddRange(GetHelpFromObject(overlay));
                }
            }

            if (LastNearestObject is null)
            {
                s.AddRange(KeyHelp);
                s.AddRange(Command.DefaultMouseHelp);
                s.Add(HelpEntry.For(HelpInput.RightDoubleClick, "Open context menu for annotation"));
            }
            else
            {
                // The help window lists these under Mouse over; entries the object already tagged keep their section
                s.AddRange(GetHelpFromObject(LastNearestObject)
                    .Select(e => e.Section == HelpSection.Command ? e.In(HelpSection.MouseOver) : e));
            }

            return [.. s];
        }

        private static HelpEntry[] GetHelpFromObject(object obj) => obj is IHelp help ? help.Help : [];

        protected object NearestObjectAtPositionAcrossAllExtensions(Vector2 WorldPosition)
        {
            object? nearest_obj = null;
            double distance = double.MaxValue;
            foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
            {
                object? nearObj = overlay.ObjectAtPosition(WorldPosition, out double newDistance);
                    if (nearObj != null && newDistance < distance)
                    {
                        nearest_obj = nearObj;
                        distance = newDistance;
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
            ObservableHelp.Clear();
            foreach (HelpEntry entry in Help)
            {
                ObservableHelp.Add(entry);
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
            Vector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
            double distance = double.MaxValue;
            object? context_obj = null;

            // Preferential overlay double-click (e.g. auto-polygonize rings) before
            // ObjectAtPosition so hollow hits do not steal single-click edit of annotations.
            if (Parent.ShowOverlays && ExtensionManager.SectionOverlays != null)
            {
                foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                {
                    if (overlay is IHandleOverlayMouseDoubleClick preferential &&
                        preferential.TryHandleMouseDoubleClick(e.Button, WorldPosition))
                    {
                        return;
                    }
                }
            }

            if (Parent.ShowOverlays)
            {
                foreach (ISectionOverlayExtension overlay in ExtensionManager.SectionOverlays)
                {
                    object? nearObj = overlay.ObjectAtPosition(WorldPosition, out double newDistance);
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
                ContextMenuStrip? menu = null;
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
