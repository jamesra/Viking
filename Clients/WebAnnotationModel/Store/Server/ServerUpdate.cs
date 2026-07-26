using System;

namespace WebAnnotationModel.ServerInterface
{
    public interface IServerUpdate<out KEY, out SERVER_OBJECT>
    {
        /// <summary>
        /// When this update was executed on the server
        /// </summary>
        DateTime QueryTime { get; }
        /// <summary>
        /// Objects returned by the server, they may be new or updates or unchanged.
        /// </summary>
        SERVER_OBJECT NewOrUpdated { get; }
        /// <summary>
        /// Objects the server is reporting deleted
        /// </summary>
        KEY[] DeletedIDs { get; }
    }

    public readonly struct ServerUpdate<KEY, SERVER_OBJECT> : IServerUpdate<KEY, SERVER_OBJECT>
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

        DateTime IServerUpdate<KEY, SERVER_OBJECT>.QueryTime => QueryTime;
        SERVER_OBJECT IServerUpdate<KEY, SERVER_OBJECT>.NewOrUpdated => NewOrUpdated;
        KEY[] IServerUpdate<KEY, SERVER_OBJECT>.DeletedIDs => DeletedIDs;
    }
}
