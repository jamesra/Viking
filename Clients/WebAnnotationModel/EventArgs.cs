using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebAnnotationModel
{
    /// <summary>
    /// Fires when a key is added or removed from a store class
    /// It is currently used with LocationStore
    /// Add is called after the object is added to the store
    /// Remove is called before the object is removed from the store
    /// </summary>
    public class AddUpdateRemoveKeyEventArgs : System.EventArgs
    {
        public enum Action {ADD, UPDATE, REMOVE};
        public readonly Action ChangeAction; 
        public readonly long ID; 

        public AddUpdateRemoveKeyEventArgs(long ID, Action action)
        {
            this.ChangeAction = action; 
            this.ID = ID;
        }
    }
    public delegate void AddUpdateRemoveKeyEventHandler(object sender, AddUpdateRemoveKeyEventArgs e);

    /// <summary>
    /// Fires when a key is added or removed from a store class
    /// It is currently used with LocationStore
    /// Add is called after the object is added to the store
    /// Remove is called before the object is removed from the store
    /// </summary>
    public class OnAllUpdatesCompletedEventArgs : System.EventArgs
    {
        public long? SectionNumber = new long?();
        public object[] Objects; 

        public OnAllUpdatesCompletedEventArgs()
        { 
        }

        public OnAllUpdatesCompletedEventArgs(long sectionnumber)
        {
            this.SectionNumber = new long?(sectionnumber); 
        }

        public OnAllUpdatesCompletedEventArgs(long sectionnumber, object[] objects)
            : this(sectionnumber)
        {
            this.Objects = objects; 
        }

        public OnAllUpdatesCompletedEventArgs(object[] objects)
        {
            this.Objects = objects;
        }
    }
    public delegate void OnAllUpdatesCompletedEventHandler(object sender, OnAllUpdatesCompletedEventArgs e);


}
