using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 

using WebAnnotation.Service;
using System.Drawing;
using System.Windows.Forms;

using Common.UI; 

namespace WebAnnotation.Objects
{
    public class StructureObj : WCFObjBaseWithParent<Structure, StructureObj>
    {
        [Column("Notes")]
        public string Notes
        {
            get { return Data.Notes; }
            set
            {
                Data.Notes = value;
                SetDBActionForChange();
                ValueChangedEvent("Notes");
            }
        }

        [Column("Verified")]
        public bool Verified
        {
            get { return Data.Verified; }
            set
            {
                Data.Verified = value;
                SetDBActionForChange();
                ValueChangedEvent("Verified");
            }
        }

        [Column("Confidence")]
        public double Confidence
        {
            get { return Data.Confidence; }
            set
            {
                Data.Confidence = value;
                SetDBActionForChange();
                ValueChangedEvent("Confidence");
            }
        }


        [Column("Tags")]
        public string[] Tags
        {
            get { return Data.Tags; }
            set
            {
                Data.Tags = value;
                
                //Refresh the tags
                SetDBActionForChange();
                ValueChangedEvent("Tags");
            }
        }

        [Column("InfoLabel")]
        public string InfoLabel
        {
            get { return Data.Label; }
            set
            {
                Data.Label = value;
                //Refresh the tags
                SetDBActionForChange();
                ValueChangedEvent("Label");
            }
        }

        public StructureLink[] Links
        {
            get { return Data.Links; }
        }

        /// <summary>
        /// Adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public void AddLink(StructureLink link)
        {
            List<StructureLink> listLinks = Data.Links.ToList<StructureLink>();
            listLinks.Add(link);
            Data.Links = listLinks.ToArray();
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public void RemoveLink(StructureLink link)
        {
            List<StructureLink> listLinks = Data.Links.ToList<StructureLink>();
            for(int i = 0; i < listLinks.Count; i++)
            {
                StructureLink item = listLinks[i]; 
                if(item.SourceID == link.SourceID && item.TargetID == link.TargetID)
                {
                    listLinks.RemoveAt(i);
                    i--; 
                }
            }
            Data.Links = listLinks.ToArray();
        }

        public StructureObj()
        {

        }

        public StructureObj(StructureTypeObj type)
        {
            this.Data = new Structure();
            InitNewData(type);
        }

        protected void InitNewData(StructureTypeObj type)
        {
            this.Data.DBAction = DBACTION.INSERT;
            
            this.Data.ID = Store.Structures.GetTempID();
            this.Data.TypeID = type.ID;
            Debug.Assert(type.ID >= 0);
            this.Data.Notes = "";
            this.Data.Tags = new String[0]; 
            this.Data.Confidence = 0.5;
            this.Data.ParentID = new long?();
        }

        private StructureTypeObj _Type = null;
        public StructureTypeObj Type
        {
            get
            {
                if (_Type == null)
                {
                    _Type = Store.StructureTypes.GetObjectByID(Data.TypeID);
                }
                return _Type; 
            }
            set
            {
                Debug.Assert(value != null);
                if (value != null)
                {
                    Data.TypeID = value.ID;
                    _Type = value;

                    SetDBActionForChange();

                    ValueChangedEvent("Type");
                }
            }
        }

        protected override StructureObj OnMissingParent()
        {
            return Store.Structures.GetObjectByID(ParentID.Value, true); 
        }

        #region IUIObject Members

        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                menu.MenuItems.Add("Delete", ContextMenu_OnDelete);
                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                return menu;
            }
        }

        public override Image SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        public override string ToolTip
        {
            get { throw new NotImplementedException(); }
        }

        public override void Save()
        {
            Store.Structures.Save();
        }

        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            return new Viking.UI.Controls.GenericTreeNode(this); 
        }

        public override int TreeImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        public override int TreeSelectedImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        public override Type[] AssignableParentTypes
        {
            get { return new System.Type[] { typeof(StructureObj) }; }
        }

    

        #endregion

        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public override void Delete()
        {
            StructureObj OriginalParent = this.Parent; 
            this.Parent = null;    

            DBACTION originalAction = this.DBAction;
            this.DBAction = DBACTION.DELETE;

            bool success = Store.Structures.Save();
            if (!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
                this.Parent = OriginalParent; 
            }
        }

        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            if (OnCreate != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
            }
        }
        public static event EventHandler Create
        {
            add { OnCreate += value; }
            remove { OnCreate -= value; }
        }
    }
}
