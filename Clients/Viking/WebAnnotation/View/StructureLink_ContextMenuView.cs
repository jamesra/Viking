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

        public static ContextMenuStrip ContextMenuGenerator(IViewStructureLink link)
        {
            StructureLink_CanvasContextMenuView contextMenuView = new(link.Key);
            return contextMenuView.ContextMenu;
        }

        public System.Windows.Forms.ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();

                if (!modelObj.Bidirectional)
                {
                    ToolStripMenuItem menuFlip = new("Flip Direction");
                    menuFlip.Click += ContextMenu_OnFlip;
                    menu.Items.Add(menuFlip);
                }

                ToolStripMenuItem menuBidirectional = new("Bidirectional")
                {
                    Checked = modelObj.Bidirectional
                };
                menuBidirectional.Click += ContextMenu_OnBidirectional;
                menu.Items.Add(menuBidirectional);

                menu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem menuDelete = new("Delete");
                menuDelete.Click += ContextMenu_OnDelete;
                menu.Items.Add(menuDelete);

                return menu;
            }
        }

        protected void ContextMenu_OnFlip(object sender, EventArgs e)
        {
            Store.StructureLinks.Remove(modelObj);
            try
            {
                Store.StructureLinks.Save();

                StructureLinkObj newLink = new(TargetID, SourceID, Bidirectional);
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

                StructureLinkObj newLink = new(SourceID, TargetID, !Bidirectional);
                Store.StructureLinks.Create(newLink);
                //              this.modelObj = newLink;
                //CreateView(newLink);
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e) => Delete();

        public void Delete()
        {
            Store.StructureLinks.Remove(modelObj);
            Store.StructureLinks.Save();
        }
    }
}
