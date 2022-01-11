using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Viking.AnnotationServiceTypes;

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
                return new List<IAction>();

            List<IAction> listAction = new List<IAction>();
            listAction.AddRange(IdentifyPossibleLocationLinkActions(log_entries, origin_ID));
            listAction.AddRange(IdentifyPossibleStructureLinkActions(log_entries, origin_ID));
            return listAction;
        }


        public static List<IAction> IdentifyPossibleStructureLinkActions(this IReadOnlyList<LocationInteractionLogEvent> log_entries, long origin_ID)
        {
            List<IAction> listActions = new List<IAction>();
            LocationObj origin_loc = Store.Locations.GetObjectByID(origin_ID);

            long origin_struct_id = origin_loc.ParentID.Value;

            //Filter any entries with the same Structure ID
            var other_structure_entries = log_entries.Where(e => e.location != null).ToArray();

            //Find the first entry with the origin ID. 
            int iStart = other_structure_entries.Length;
            for (int i = 0; i < other_structure_entries.Length; i++)
            {
                if (other_structure_entries[i].location.ID == origin_ID)
                {
                    iStart = i;
                    break;
                }
            }

            bool inside_origin_shape = log_entries[iStart].Interaction != AnnotationRegionInteraction.EXIT;

            //The path I'm looking for is a "scribble" that passes between the source and target structure 3 or 4 times.
            //candidate_touch_count counts the number of time the path passes between each structure and the origin
            //When I wrote this 3 passes was a one-directional line, 4 passes was bidirectional.

            Dictionary<long, int> candidate_touch_count = new Dictionary<long, int>();

            for (int i = iStart + 1; i < other_structure_entries.Length; i++)
            {
                LocationObj other_location = other_structure_entries[i].location;
                long other_struct_id = other_structure_entries[i].location.ParentID.Value;
                if (other_structure_entries[i].Interaction == AnnotationRegionInteraction.ENTER)
                {
                    if (other_struct_id != origin_struct_id)
                    {
                        //We've touched another structure.  Create or update its count
                        if (candidate_touch_count.ContainsKey(other_location.ID))
                        {
                            candidate_touch_count[other_location.ID] = candidate_touch_count[other_location.ID] + 1;
                        }
                        else
                        {
                            candidate_touch_count.Add(other_location.ID, 1);
                        }
                    }
                    else
                    {
                        //We've returned to the origin structure, so add 1 to the touch count for all structures we've touched so far
                        foreach (long key in candidate_touch_count.Keys.ToArray())
                        {
                            candidate_touch_count[key] = candidate_touch_count[key] + 1;
                        }
                    }
                }
            }

            //OK, if there are an odd number of 3 or more passes add a directional link. Otherwise a bidirectional link
            foreach (long key in candidate_touch_count.Keys)
            {
                int touch_count = candidate_touch_count[key];
                if (touch_count < 3)
                    continue; //Not enough touches to create a link

                LocationObj other_location = Store.Locations[key];
                long other_struct_id = other_location.ParentID.Value;

                StructureLinkKey link_candidate;

                if (touch_count % 2 > 0)
                {
                    link_candidate = new StructureLinkKey(origin_struct_id, other_struct_id, false);
                }
                else
                {
                    link_candidate = new StructureLinkKey(origin_struct_id, other_struct_id, true);
                }

                //Add the link if it does not exist
                if (false == Store.StructureLinks.Contains(link_candidate))
                {
                    var action = new LinkStructureAction(origin_loc, other_location, link_candidate.Bidirectional);
                    listActions.Add(action);
                }
            }

            return listActions;
        }

        public static List<IAction> IdentifyPossibleLocationLinkActions(this IReadOnlyList<LocationInteractionLogEvent> log_entries, long origin_ID)
        {
            LocationObj origin_loc = Store.Locations.GetObjectByID(origin_ID);

            var candidates = log_entries.Where(e => e.location != null &&
                                       e.location.ParentID == origin_loc.ParentID &&
                                       e.location.Z != origin_loc.Z);

            //Identify all location links that do not exist in the local store already
            var non_existing_links = candidates.Select(c => new LocationLinkKey(c.location.ID, origin_ID))
                                               .Distinct()
                                               .Where(ll => Store.LocationLinks.Contains(ll) == false).ToArray();

            return non_existing_links.Select(ll => new LinkLocationAction(ll) as IAction).ToList();
        }


    }
}
