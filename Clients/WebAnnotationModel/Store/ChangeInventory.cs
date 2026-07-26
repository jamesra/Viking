using System.Collections.Generic;

namespace WebAnnotationModel
{

    /// <summary>
    /// Groups the objects according to how the store saw them during an operation.  Used to 
    /// store up all changes so a single set of collection changed events can be sent
    /// </summary>
    /// <typeparam name="OBJECT"></typeparam>
    public class ChangeInventory<OBJECT>
    {
        /// <summary>
        /// Objects freshly added to the store
        /// </summary>
        public readonly List<OBJECT> AddedObjects;

        /// <summary>
        /// Objects that existed in the store but had some properties updated
        /// </summary>
        public readonly List<OBJECT> UpdatedObjects;

        /// <summary>
        /// Objects we deleted from the store
        /// </summary>
        public readonly List<OBJECT> DeletedObjects;

        /// <summary>
        /// An object that was in the store but was removed and replaced with a new object
        /// </summary>
        public readonly List<OBJECT> OldObjectsReplaced;

        /// <summary>
        /// Objects that are now in the store and replaced an object that existed previously, common when server sends a new ID on update
        /// </summary>
        public readonly List<OBJECT> NewObjectReplacements;

        /// <summary>
        /// Objects that were found already existing in the store and required no updates
        /// </summary>
        public readonly List<OBJECT> UnchangedObjects;

        public ChangeInventory()
        {
            AddedObjects = new List<OBJECT>();
            UpdatedObjects = new List<OBJECT>();
            DeletedObjects = new List<OBJECT>();
            OldObjectsReplaced = new List<OBJECT>();
            NewObjectReplacements = new List<OBJECT>();
            UnchangedObjects = new List<OBJECT>();

        }

        public ChangeInventory(int numObjects)
        {
            AddedObjects = new List<OBJECT>(numObjects);
            UpdatedObjects = new List<OBJECT>(numObjects);
            DeletedObjects = new List<OBJECT>(numObjects);
            OldObjectsReplaced = new List<OBJECT>(numObjects);
            NewObjectReplacements = new List<OBJECT>(numObjects);
            UnchangedObjects = new List<OBJECT>(numObjects);
        }

        /// <summary>
        /// Return a concatenation of all affected objects minus objects that were deleted
        /// </summary>
        /// <returns></returns>
        public List<OBJECT> ObjectsInStore
        {
            get
            {
                List<OBJECT> listObjects = new List<OBJECT>(AddedObjects.Count + UpdatedObjects.Count + NewObjectReplacements.Count);
                listObjects.AddRange(AddedObjects);
                listObjects.AddRange(UpdatedObjects);
                listObjects.AddRange(NewObjectReplacements);
                listObjects.AddRange(UnchangedObjects);
                return listObjects;
            }
        }

        /// <summary>
        /// Add all elements from another inventory to our own
        /// </summary>
        /// <param name="inventory"></param>
        public void Add(ChangeInventory<OBJECT> inventory)
        {
            AddedObjects.AddRange(inventory.AddedObjects);
            UpdatedObjects.AddRange(inventory.UpdatedObjects);
            DeletedObjects.AddRange(inventory.DeletedObjects);
            OldObjectsReplaced.AddRange(inventory.OldObjectsReplaced);
            NewObjectReplacements.AddRange(inventory.NewObjectReplacements);
            UnchangedObjects.AddRange(inventory.UnchangedObjects);
        }

        public static ChangeInventory<OBJECT> operator +(ChangeInventory<OBJECT> a, ChangeInventory<OBJECT> b)
        {
            var c = new ChangeInventory<OBJECT>();
            c.Add(a);
            c.Add(b);

            return c;
        }
    }
}
