using System.Collections.Generic;
using System.Linq;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Decides whether an auto-polygonize result that just came back is older than what is already
    /// published for the same locations. Every segmentation request takes a ticket from one increasing
    /// counter when it starts, and the proposal it produces carries that ticket. Results complete in
    /// any order (single circles, overlap groups and reruns are all fire-and-forget), so the ticket,
    /// not completion order and not mask size, says which answer reflects the newest request.
    /// </summary>
    internal static class RequestSupersession
    {
        /// <summary>Ticket value for a proposal that never went through a tracked request. Never superseded.</summary>
        public const long Untracked = 0;

        /// <summary>
        /// True when any published proposal shares a location with <paramref name="incomingIds"/> and
        /// carries a newer ticket than <paramref name="incomingTicket"/>.
        /// Only published proposals count: a newer request that is still running, or that failed,
        /// must not cost the older result, otherwise a failed group would leave its circles blank.
        /// </summary>
        public static bool IsSuperseded(
            long incomingTicket,
            IReadOnlyCollection<long> incomingIds,
            IEnumerable<(long Ticket, IReadOnlyCollection<long> LocationIds)> published)
        {
            if (incomingTicket == Untracked)
                return false;

            foreach ((long ticket, IReadOnlyCollection<long> ids) in published)
            {
                if (ticket != Untracked && ticket > incomingTicket && ids.Any(incomingIds.Contains))
                    return true;
            }

            return false;
        }
    }
}
