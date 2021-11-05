using System.Collections.Generic;
using System;
using System.Collections;

namespace WebAnnotationModel
{ 
    public readonly struct UpdateResults<KEY, OBJECT>
    {
        /// <summary>
        /// Objects freshly added to the store
        /// </summary>
        public readonly OBJECT[] AddedObjects;

        /// <summary>
        /// Objects that existed in the store but with updated properties
        /// </summary>
        public readonly OBJECT[] UpdatedObjects;

        /// <summary>
        /// Objects deleted from the store
        /// </summary>
        public readonly KEY[] DeletedIDs;

        public UpdateResults(OBJECT[] added = null, OBJECT[] updated = null, KEY[] deleted = null)
        {
            AddedObjects = added ?? Array.Empty<OBJECT>();
            UpdatedObjects = updated ?? Array.Empty<OBJECT>();
            DeletedIDs = deleted ?? Array.Empty<KEY>(); 
        }

        public UpdateResults(OBJECT added = default, OBJECT updated = default, KEY deleted = default)
        {
            AddedObjects = added != null ? new OBJECT[] {
                added
            } : Array.Empty<OBJECT>();
            UpdatedObjects = updated != null ? new OBJECT[] { updated } : Array.Empty<OBJECT>();
            DeletedIDs = deleted != null ? new KEY[] {
                deleted } : Array.Empty<KEY>();
        }
    }
}
