using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using System.Windows.Forms;
using Viking.Common;

namespace WebAnnotation.View
{
    class StructureLink_CanvasContextMenuView : IContextMenu
    {
        public StructureLinkKey linkKey;
        public StructureLinkObj modelObj;

        public long SourceID
        {
            get
            {
                return modelObj.SourceID;
            }
        }

        public long TargetID
        {
            get
            {
                return modelObj.TargetID;
            }
        }

        public bool Bidirectional
        {
            get { return modelObj.Bidirectional; } 
        }

        public StructureLink_CanvasContextMenuView(StructureLinkObj obj)
        {
            this.modelObj = obj;
            this.linkKey = obj.ID;
        }

        public StructureLink_CanvasContextMenuView(StructureLinkKey link)
        {
            this.linkKey = link;
            this.modelObj = Store.StructureLinks[link];
        }
         
        public System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                MenuItem menuFlip = new MenuItem("Flip Direction", ContextMenu_OnFlip);

                MenuItem menuBidirectional = new MenuItem("Bidirectional", ContextMenu_OnBidirectional);
                menuBidirectional.Checked = this.modelObj.Bidirectional;

                MenuItem menuSeperator = new MenuItem();
                MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

                if (!modelObj.Bidirectional)
                    menu.MenuItems.Add(menuFlip);

                menu.MenuItems.Add(menuBidirectional);
                menu.MenuItems.Add(menuSeperator);
                menu.MenuItems.Add(menuDelete);

                return menu;
            }
        }

        protected void ContextMenu_OnFlip(object sender, EventArgs e)
        {
            Store.StructureLinks.Remove(this.modelObj);
            bool Success = Store.StructureLinks.Save();
            if (Success)
            {
                StructureLinkObj newLink = new StructureLinkObj(this.TargetID, this.SourceID, this.Bidirectional);
                Store.StructureLinks.Create(newLink);
                //              this.modelObj = newLink;
                //CreateView(newLink);
            }
        }

        protected void ContextMenu_OnBidirectional(object sender, EventArgs e)
        {
            Store.StructureLinks.Remove(this.modelObj);
            bool Success = Store.StructureLinks.Save();
            if (Success)
            {
                StructureLinkObj newLink = new StructureLinkObj(this.TargetID, this.SourceID, !this.Bidirectional);
                Store.StructureLinks.Create(newLink);
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public void Delete()
        {
            Store.StructureLinks.Remove(this.modelObj);
            Store.StructureLinks.Save();
        }
    }
}
