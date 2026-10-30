using Viking.AnnotationServiceTypes;
using Geometry;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Viking.Common;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.ViewModel
{
    internal class SectionStructureLinkAnnotationsViewModel(SectionAnnotationsView primarySection)
    {
        /// <summary>
        /// The section that is visible
        /// </summary>
        public readonly SectionAnnotationsView PrimarySection = primarySection;

        private readonly KeyTracker<StructureLinkKey> KnownStructureLinks = new();

        /// <summary>
        /// Allows us to describe all the StructureLinks visible on a screen
        /// </summary>
        private readonly RTree.RTree<StructureLinkKey> StructureLinksSearch = new();
        private readonly ConcurrentDictionary<StructureLinkKey, StructureLinkViewModelBase> StructureLinks = new();

        /// <summary>
        /// Return all the line segments visible in the passed bounds. A link removed on another thread leaves the
        /// spatial index after the dictionary, so a key it returns can already be gone; those are skipped.
        /// </summary>
        /// <param name="scene">The scene whose visible world bounds are queried.</param>
        public List<StructureLinkViewModelBase> VisibleStructureLinks(VikingXNA.Scene scene) => [.. StructureLinks.ValuesForKeys(StructureLinksSearch.Intersects(scene.VisibleWorldBounds.ToRTreeRect(PrimarySection.SectionNumber)))
            .Where(sl => sl.IsVisible(scene))];

        internal void AddStructureLinks(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj locObj in locations)
            {
                if (!locObj.ParentID.HasValue)
                {
                    continue;
                }

                Store.Structures.TryGetObjectByID(locObj.ParentID.Value, out StructureObj parent);
                if (parent is null)
                {
                    continue;
                }

                if (parent.NumLinks > 0)
                {
                    AddStructureLinks(parent.LinksCopy);
                }
            }
        }

        internal void AddStructureLinks(IEnumerable<StructureObj> structures)
        {
            foreach (StructureObj structObj in structures)
            {
                if (structObj.NumLinks > 0)
                {
                    AddStructureLinks(structObj.LinksCopy);
                }
            }
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void AddStructureLinks(IEnumerable<StructureLinkObj> structureLinks)
        {
            foreach (StructureLinkObj structLinkObj in structureLinks)
            {
                if (structLinkObj is null)
                {
                    continue;
                }

                StructureLinkViewModelBase StructLink = CreateStructureLinkWithLocations(structLinkObj);
                if (StructLink is null)
                {
                    //Trace.WriteLine("Cannot find locations for " + structLinkObj.ToString());
                    continue;
                }


                KnownStructureLinks.TryAdd(structLinkObj.ID, () =>
                {
                    //An error can occur if two structures are linked to each other twicea, once as a source and once as a destination.
                    StructureLinks.TryAdd(structLinkObj.ID, StructLink);
                    StructureLinksSearch.TryAdd(StructLink.BoundingBox.ToRTreeRect(PrimarySection.SectionNumber), structLinkObj.ID);
                });
            }
        }

        internal void RemoveStructureLinks(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj locObj in locations)
            {
                StructureObj parent = locObj.Parent;
                if (parent is null)
                {
                    continue;
                }

                if (parent.NumLinks > 0)
                {
                    RemoveStructureLinks(parent.LinksCopy);
                }
            }
        }

        /// <summary>
        /// Re-creates the link views that <see cref="RemoveStructureLinks(IEnumerable{LocationObj})"/> dropped when
        /// <paramref name="removedLocations"/> left the section. That removal drops every link of each location's
        /// structure, so call this after the locations are gone: a link whose structures both still have locations on
        /// this section comes back between their nearest remaining locations, and a structure whose last location here
        /// was removed creates no view.
        /// </summary>
        /// <param name="removedLocations">Locations just removed from the section's view; others are ignored.</param>
        internal void RestoreStructureLinksAfterRemoval(IEnumerable<LocationObj> removedLocations)
        {
            AddStructureLinks(StructureIDsToRefreshAfterRemoval(removedLocations, PrimarySection.SectionNumber)
                .Select(id => Store.Structures.TryGetObjectByID(id, out StructureObj structure) ? structure : null)
                .Where(s => s is not null));
        }

        /// <summary>
        /// The parents of the <paramref name="removedLocations"/> on <paramref name="sectionNumber"/>, once each, so a
        /// batch delete of many locations of one structure rebuilds its links once. Removing a location on another
        /// section drops no structure link here, so those locations are skipped.
        /// </summary>
        /// <param name="removedLocations">Locations removed from the store.</param>
        /// <param name="sectionNumber">The section whose structure links are shown.</param>
        internal static IEnumerable<long> StructureIDsToRefreshAfterRemoval(IEnumerable<LocationObj> removedLocations, int sectionNumber) =>
            removedLocations.Where(l => l.Z == sectionNumber && l.ParentID.HasValue).Select(l => l.ParentID.Value).Distinct();

        internal void RemoveStructureLinks(IEnumerable<StructureObj> structures)
        {
            foreach (StructureObj structObj in structures)
            {
                if (structObj.NumLinks > 0)
                {
                    RemoveStructureLinks(structObj.LinksCopy);
                }
            }
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void RemoveStructureLinks(IEnumerable<StructureLinkObj> structureLinks)
        {
            if (structureLinks is null)
            {
                return;
            }

            foreach (StructureLinkObj structLinkObj in structureLinks)
            {
                if (structLinkObj is null)
                {
                    continue;
                }

                KnownStructureLinks.TryRemove(structLinkObj.ID, () =>
                {
                    StructureLinks.TryRemove(structLinkObj.ID, out StructureLinkViewModelBase removedLinkView);
                    //An error can occur if two structures are linked to each other twicea, once as a source and once as a destination.
                    StructureLinksSearch.Delete(structLinkObj.ID, out StructureLinkKey removedID);
                });
            }
        }

        internal StructureLinkViewModelBase CreateStructureLinkWithLocations(StructureLinkObj structLinkObj)
        {
            if (structLinkObj.SourceID == structLinkObj.TargetID)
            {
                Trace.WriteLine("Something is wrong on the server, struct ID links to itself: " + structLinkObj.SourceID.ToString());
                _ = RemoveSelfLinkAsync(structLinkObj);
                return null;
            }

            //The link may have been created to a structure on an adjacent section 
            bool Success = PrimarySection.GetLocationsForStructure(structLinkObj.SourceID, out KeyTracker<long> SourceLocationIDs);
            if (Success == false)
            {
                return null;
            }

            Success = PrimarySection.GetLocationsForStructure(structLinkObj.TargetID, out KeyTracker<long> TargetLocationIDs);
            if (Success == false)
            {
                return null;
            }

            ICollection<LocationCanvasView> SourceLocations = [.. SourceLocationIDs.ValuesCopy().Select((l_id) => PrimarySection.GetLocation(l_id)).Where(l => l != null)];
            ICollection<LocationCanvasView> TargetLocations = [.. TargetLocationIDs.ValuesCopy().Select((l_id) => PrimarySection.GetLocation(l_id)).Where(l => l != null)];

            SectionStructureLinkViewKey linkViewKey = SectionStructureLinkViewKey.CreateForNearestLocations(structLinkObj.ID, SourceLocations, TargetLocations);
            if (linkViewKey is null)
            {
                return null;
            }

            //OK, create a StructureLink between the locations
            return AnnotationViewFactory.Create(linkViewKey, PrimarySection.mapper);
        }

        static async Task RemoveSelfLinkAsync(StructureLinkObj structLinkObj)
        {
            await Store.StructureLinks.Remove(structLinkObj);
            try
            {
                await Store.StructureLinks.Save();
            }
            catch (System.Exception e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
            }
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks() => StructureLinks.Values;

        /// <summary>
        /// Links whose bounding box intersects <paramref name="bounds"/>. Like every query here, it skips links
        /// removed on another thread between the R-tree read and the dictionary lookup.
        /// </summary>
        public ICollection<StructureLinkViewModelBase> GetStructureLinks(Rectangle bounds) =>
            StructureLinks.ValuesForKeys(StructureLinksSearch.Intersects(bounds.ToRTreeRect((float)PrimarySection.SectionNumber)));

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(Vector2 point) =>
            [.. StructureLinks.ValuesForKeys(StructureLinksSearch.Intersects(point.ToRTreeRect((float)PrimarySection.SectionNumber))).Where(sl => sl.Contains(point))];

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(LineSegment line) =>
            [.. StructureLinks.ValuesForKeys(StructureLinksSearch.Intersects(line.BoundingBox.ToRTreeRect((float)PrimarySection.SectionNumber))).Where(sl => sl.Intersects(line))];
    }


}
