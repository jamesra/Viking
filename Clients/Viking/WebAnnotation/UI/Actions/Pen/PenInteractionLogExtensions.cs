using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Reusable functions for parsing InteractionLogEvent and InteractionLog
    /// </summary>
    public static class PenInteractionLogExtensions
    {
        public static List<IAction> IdentifyPossibleLinkActions(this IReadOnlyList<InteractionLogEvent> log_entries, long origin_ID)
        {
            LocationInteractionLogEvent[] converted_log = LocationInteractionLogEvent.CreateFromLog(log_entries);
            return IdentifyPossibleLinkActions(converted_log, origin_ID);
        }

        public static List<IAction> IdentifyPossibleLinkActions(this IReadOnlyList<LocationInteractionLogEvent> log_entries, long origin_ID)
        {
            //Check if the origin is even in the list of entries before going into detail
            if (false == log_entries.Any(e => e.location?.ID == origin_ID))
            {
                return [];
            }

            List<IAction> listAction =
            [
                .. IdentifyPossibleLocationLinkActions(log_entries, origin_ID),
                .. IdentifyPossibleStructureLinkActions(log_entries, origin_ID),
            ];
            return listAction;
        }


        /// <summary>
        /// Proposes a structure link between the origin location's structure and each other structure
        /// the pen path passed to and from at least three times. An odd pass count proposes a directional
        /// link, an even count a bidirectional one. Links already in the store are skipped.
        /// </summary>
        public static List<IAction> IdentifyPossibleStructureLinkActions(this IReadOnlyList<LocationInteractionLogEvent> log_entries, long origin_ID)
        {
            List<IAction> listActions = [];
            if (!Store.Locations.TryGetObjectByID(origin_ID, out LocationObj origin_loc) || origin_loc is null)
                return listActions;

            long origin_struct_id = origin_loc.ParentID.Value;

            Dictionary<long, (LocationObj Target, int Passes)> passes = CountStructurePasses(
                [.. log_entries.Select(e => (e.Interaction, e.location))], origin_ID, origin_struct_id);

            //OK, if there are an odd number of 3 or more passes add a directional link. Otherwise a bidirectional link
            foreach ((LocationObj other_location, int touch_count) in passes.Values)
            {
                if (touch_count < 3)
                {
                    continue; //Not enough touches to create a link
                }

                long other_struct_id = other_location.ParentID.Value;

                StructureLinkKey link_candidate = touch_count % 2 > 0
                    ? new StructureLinkKey(origin_struct_id, other_struct_id, false)
                    : new StructureLinkKey(origin_struct_id, other_struct_id, true);

                //Add the link if it does not exist
                if (false == Store.StructureLinks.Contains(link_candidate))
                {
                    LinkStructureAction action = new(origin_loc, other_location, link_candidate.Bidirectional);
                    listActions.Add(action);
                }
            }

            return listActions;
        }

        /// <summary>
        /// Counts how many times the pen path passed between the origin structure and each other structure,
        /// starting at the first event on the origin location. Entering another structure counts one pass for
        /// it; re-entering the origin structure counts one pass for every structure touched so far.
        /// </summary>
        /// <param name="events">Interaction log in path order; events with a null location are ignored.</param>
        /// <param name="origin_ID">Location the pen path is acting on.</param>
        /// <param name="origin_struct_id">Structure that owns <paramref name="origin_ID"/>.</param>
        /// <returns>Pass counts keyed by structure ID, each with the first location touched on that structure.</returns>
        internal static Dictionary<long, (LocationObj Target, int Passes)> CountStructurePasses(
            IReadOnlyList<(AnnotationRegionInteraction Interaction, LocationObj? Location)> events, long origin_ID, long origin_struct_id)
        {
            (AnnotationRegionInteraction Interaction, LocationObj Location)[] located = [.. events.Where(e => e.Location != null).Select(e => (e.Interaction, e.Location!))];

            //Find the first entry with the origin ID.
            int iStart = located.Length;
            for (int i = 0; i < located.Length; i++)
            {
                if (located[i].Location.ID == origin_ID)
                {
                    iStart = i;
                    break;
                }
            }

            //The path I'm looking for is a "scribble" that passes between the source and target structure 3 or 4 times.
            //When I wrote this 3 passes was a one-directional line, 4 passes was bidirectional.
            Dictionary<long, (LocationObj Target, int Passes)> candidate_touch_count = [];

            for (int i = iStart + 1; i < located.Length; i++)
            {
                if (located[i].Interaction != AnnotationRegionInteraction.ENTER)
                {
                    continue;
                }

                LocationObj other_location = located[i].Location;
                long other_struct_id = other_location.ParentID.Value;
                if (other_struct_id != origin_struct_id)
                {
                    //We've touched another structure.  Create or update its count. Passes belong to the structure,
                    //so its locations count together; the first location touched represents it in the link action.
                    candidate_touch_count[other_struct_id] = candidate_touch_count.TryGetValue(other_struct_id, out var entry)
                        ? (entry.Target, entry.Passes + 1)
                        : (other_location, 1);
                }
                else
                {
                    //We've returned to the origin structure, so add 1 to the touch count for all structures we've touched so far
                    foreach (long key in candidate_touch_count.Keys.ToArray())
                    {
                        var entry = candidate_touch_count[key];
                        candidate_touch_count[key] = (entry.Target, entry.Passes + 1);
                    }
                }
            }

            return candidate_touch_count;
        }

        public static List<IAction> IdentifyPossibleLocationLinkActions(this IReadOnlyList<LocationInteractionLogEvent> log_entries, long origin_ID)
        {
            if (!Store.Locations.TryGetObjectByID(origin_ID, out LocationObj origin_loc) || origin_loc is null)
                return [];

            IEnumerable<LocationInteractionLogEvent> candidates = log_entries.Where(e => e.location != null &&
                                       e.location.ParentID == origin_loc.ParentID &&
                                       e.location.Z != origin_loc.Z);

            //Identify all location links that do not exist in the local store already
            LocationLinkKey[] non_existing_links = [.. candidates.Select(c => new LocationLinkKey(c.location.ID, origin_ID))
                                               .Distinct()
                                               .Where(ll => Store.LocationLinks.Contains(ll) == false)];

            return [.. non_existing_links.Select(ll => new LinkLocationAction(ll) as IAction)];
        }


    }
}
