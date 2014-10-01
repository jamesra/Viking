using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using WebAnnotationModel;
using WebAnnotationModel.Service;
using WebAnnotationModel.Objects; 

namespace WebAnnotationModel
{
    public class StructureTypeObj : WCFObjBaseWithParent<long, StructureType, StructureTypeObj>
    {
        public override long ID
        {
            get { return Data.ID; }
        }

        /// <summary>
        /// The ID for newo bjects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.
        /// </summary>
        /// <returns></returns>
        protected override int GenerateHashCode()
        {
            return (int)(ID % int.MaxValue);
        }
        
        public override long? ParentID
        {
            get { return Data.ParentID; }
            set { Data.ParentID = value; }
        }

        public override string ToString()
        {
            return this.Name; 
        }

        public string Name
        {
            get { return Data.Name; }
            set
            {
                  OnPropertyChanging("Name");
                  Data.Name = value; 
                  SetDBActionForChange();
                  OnPropertyChanged("Name");
            }
        }

        public string Notes
        {
            get { return Data.Notes; }
            set
            {
                OnPropertyChanging("Notes");
                Data.Notes = value;
                SetDBActionForChange();
                OnPropertyChanged("Notes");
            }
        }

        public int Color
        {
            get { return Data.Color; }
            set
            {
                if (Data.Color == value)
                    return;
                OnPropertyChanging("Color");
                Data.Color = value; 
                SetDBActionForChange();
                OnPropertyChanged("Color");
            }
        }

        public string Code
        {
            get { return Data.Code; }
            set
            {
                OnPropertyChanging("Code");
                Data.Code = value;
                SetDBActionForChange();
                OnPropertyChanged("Code");
            }
        }
        
        public StructureTypeObj()
        {
            if(this.Data == null)
                this.Data = new StructureType();

            this.Data.DBAction = DBACTION.INSERT;
            this.Data.Name = "New Structure Type";
            this.Data.MarkupType = "Point";
            this.Data.ID = Store.StructureTypes.GetTempKey();
            this.Data.Tags = new String[0];
            this.Data.StructureTags = new String[0];
            this.Data.Code = "NoCode";
        }

        public StructureTypeObj(StructureType data)
        {
            this.Data = data;
            this.Data.Code = this.Data.Code.Trim();
        }

        public StructureTypeObj(StructureTypeObj parent) : this()
        {
            if(this.Data == null)
                this.Data = new StructureType();

            if (parent != null)
            {
                this.Data.ParentID = parent.ID;
            }
        }

        protected override StructureTypeObj OnMissingParent()
        {
            return Store.StructureTypes.GetObjectByID(this.ParentID.Value, true); 
        }

        /*
        public override void Delete()
        {
            StructureTypeObj OriginalParent = this.Parent;
            this.Parent = null;

            DBACTION originalAction = this.DBAction;
            this.DBAction = DBACTION.DELETE;

            bool success = Store.StructureTypes.Save();
            if (!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
                this.Parent = OriginalParent;
            }
            
            Viking.UI.State.SelectedObject = null;             
        }
         */

    }
}
