using System;
using System.Windows.Forms;
using Viking.Common;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    internal class StructureLink_CanvasContextMenuView : IContextMenu
    {
        public StructureLinkKey linkKey;
        public StructureLinkObj modelObj;


        public long SourceID => modelObj.SourceID;

        public long TargetID => modelObj.TargetID;

        public bool Bidirectional => modelObj.Bidirectional;

        public StructureLink_CanvasContextMenuView(StructureLinkObj obj)
        {
            modelObj = obj;
            linkKey = obj.ID;
        }

        public StructureLink_CanvasContextMenuView(StructureLinkKey link)
        {
            linkKey = link;
            modelObj = Store.StructureLinks[link];
        }

        public static ContextMenu ContextMenuGenerator(IViewStructureLink link)
        {
            StructureLink_CanvasContextMenuView contextMenuView = new StructureLink_CanvasContextMenuView(link.Key);
            return contextMenuView.ContextMenu;
        }

        public System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                MenuItem menuFlip = new MenuItem("Flip Direction", ContextMenu_OnFlip);

                MenuItem menuBidirectional = new MenuItem("Bidirectional", ContextMenu_OnBidirectional)
                {
                    Checked = modelObj.Bidirectional
                };

                MenuItem menuSeperator = new MenuItem();
                MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

                if (!modelObj.Bidirectional)
                {
                    menu.MenuItems.Add(menuFlip);
                }

                menu.MenuItems.Add(menuBidirectional);
                menu.MenuItems.Add(menuSeperator);
                menu.MenuItems.Add(menuDelete);

                return menu;
            }
        }

        protected void ContextMenu_OnFlip(object sender, EventArgs e)
        {
            Store.StructureLinks.Remove(modelObj);
            try
            {
                Store.StructureLinks.Save();

                StructureLinkObj newLink = new StructureLinkObj(TargetID, SourceID, Bidirectional);
                Store.StructureLinks.Create(newLink);
                //              this.modelObj = newLink;
                //CreateView(newLink);
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
            }
        }

        protected void ContextMenu_OnBidirectional(object sender, EventArgs e)
        {
            Store.StructureLinks.Remove(modelObj);
            try
            {
                Store.StructureLinks.Save();

                StructureLinkObj newLink = new StructureLinkObj(SourceID, TargetID, !Bidirectional);
                Store.StructureLinks.Create(newLink);
                //              this.modelObj = newLink;
                //CreateView(newLink);
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public void Delete()
        {
            Store.StructureLinks.Remove(modelObj);
            Store.StructureLinks.Save();
        }
    }
}
