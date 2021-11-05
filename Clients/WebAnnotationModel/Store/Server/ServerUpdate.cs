using System;

namespace WebAnnotationModel
{
    public readonly struct ServerUpdate<KEY, SERVER_OBJECT>
    {
        /// <summary>
        /// When this update was performed on the server
        /// </summary>
        public readonly DateTime QueryTime;

        /// <summary>
        /// Objects returned by the server, they may be new or updates or unchanged.
        /// </summary>
        public readonly SERVER_OBJECT NewOrUpdated;

        /// <summary>
        /// Objects the server is reporting deleted
        /// </summary>
        public readonly KEY[] DeletedIDs;
          
        public ServerUpdate(DateTime? queryTime = null, SERVER_OBJECT obj = default, KEY deleted = default)
        {
            QueryTime = queryTime ?? DateTime.UtcNow;

            NewOrUpdated = obj;
            DeletedIDs = deleted != null ? new KEY[] {
                deleted } : Array.Empty<KEY>();
        }

        public ServerUpdate(DateTime? queryTime = null, SERVER_OBJECT obj = default, KEY[] deleted = default)
        {
            QueryTime = queryTime ?? DateTime.UtcNow;

            NewOrUpdated = obj;
            DeletedIDs = deleted ?? Array.Empty<KEY>();
        }

        public ServerUpdate(DateTime? queryTime = null, KEY[] deleted = default)
        {
            QueryTime = queryTime ?? DateTime.UtcNow;

            NewOrUpdated = default;
            DeletedIDs = deleted ?? Array.Empty<KEY>();
        }
    }
}
