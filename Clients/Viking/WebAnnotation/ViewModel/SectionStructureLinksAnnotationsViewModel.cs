using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viking.Common;
using Geometry;
using System.Collections.Concurrent;
using Viking.VolumeModel;
using WebAnnotationModel;
using Viking.ViewModels;
using System.Diagnostics;
using WebAnnotation.View;

namespace WebAnnotation.ViewModel
{

    class SectionStructureLinkAnnotationsViewModel
    {
        /// <summary>
        /// The section that is visible
        /// </summary>
        public SectionAnnotationsView PrimarySection;

        private KeyTracker<StructureLinkKey> KnownStructureLinks = new KeyTracker<StructureLinkKey>();

        /// <summary>
        /// Allows us to describe all the StructureLinks visible on a screen
        /// </summary>
        private RTree.RTree<StructureLinkKey> StructureLinksSearch = new RTree.RTree<StructureLinkKey>();
        private ConcurrentDictionary<StructureLinkKey, StructureLinkViewModelBase> StructureLinks = new ConcurrentDictionary<StructureLinkKey, StructureLinkViewModelBase>();

        public SectionStructureLinkAnnotationsViewModel(SectionAnnotationsView primarySection)
        {
            this.PrimarySection = primarySection;
        }

        /// <summary>
        /// Return all the line segments visible in the passed bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public List<StructureLinkViewModelBase> VisibleStructureLinks(VikingXNA.Scene scene)
        {
            return StructureLinksSearch.Intersects(scene.VisibleWorldBounds.ToRTreeRect(this.PrimarySection.SectionNumber)).Select((sl_key) => this.StructureLinks[sl_key]).Where(sl => sl != null && sl.IsVisible(scene)).ToList();
        }

        internal void AddStructureLinks(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj locObj in locations)
            {
                if (!locObj.ParentID.HasValue)
                    continue;

                StructureObj parent = Store.Structures.GetObjectByID(locObj.ParentID.Value, true);//locObj.Parent;
                if (parent == null)
                    continue;

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
                    AddStructureLinks(structObj.LinksCopy);
            }
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void AddStructureLinks(IEnumerable<StructureLinkObj> structureLinks)
        {
            foreach (StructureLinkObj structLinkObj in structureLinks)
            {
                if (structLinkObj == null)
                    continue;

                StructureLinkViewModelBase StructLink = CreateStructureLinkWithLocations(structLinkObj);
                if (StructLink == null)
                {
                    //Trace.WriteLine("Cannot find locations for " + structLinkObj.ToString());
                    continue;
                }


                KnownStructureLinks.TryAdd(structLinkObj.ID, () =>
                {
                    //An error can occur if two structures are linked to each other twicea, once as a source and once as a destination.
                    StructureLinks.TryAdd(structLinkObj.ID, StructLink);
                    StructureLinksSearch.TryAdd(StructLink.BoundingBox.ToRTreeRect(this.PrimarySection.SectionNumber), structLinkObj.ID);
                });
            }
        }

        internal void RemoveStructureLinks(IEnumerable<LocationObj> locations)
        {
            foreach (LocationObj locObj in locations)
            {
                StructureObj parent = locObj.Parent;
                if (parent == null)
                    continue;

                if (parent.NumLinks > 0)
                    RemoveStructureLinks(parent.LinksCopy);
            }
        }

        internal void RemoveStructureLinks(IEnumerable<StructureObj> structures)
        {
            foreach (StructureObj structObj in structures)
            {
                if (structObj.NumLinks > 0)
                    RemoveStructureLinks(structObj.LinksCopy);
            }
        }

        /// <summary>
        /// All locations which are linked get a line between them
        /// </summary>
        internal void RemoveStructureLinks(IEnumerable<StructureLinkObj> structureLinks)
        {
            if (structureLinks == null)
                return;

            foreach (StructureLinkObj structLinkObj in structureLinks)
            {
                if (structLinkObj == null)
                    continue;

                KnownStructureLinks.TryRemove(structLinkObj.ID, () =>
                {
                    StructureLinkViewModelBase removedLinkView;
                    StructureLinks.TryRemove(structLinkObj.ID, out removedLinkView);
                    //An error can occur if two structures are linked to each other twicea, once as a source and once as a destination.
                    StructureLinkKey removedID;
                    StructureLinksSearch.Delete(structLinkObj.ID, out removedID);
                });
            }
        }

        internal StructureLinkViewModelBase CreateStructureLinkWithLocations(StructureLinkObj structLinkObj)
        {
            if (structLinkObj.SourceID == structLinkObj.TargetID)
            {
                Trace.WriteLine("Something is wrong on the server, struct ID links to itself: " + structLinkObj.SourceID.ToString());
                Store.StructureLinks.Remove(structLinkObj);
                Store.StructureLinks.Save();
                return null;
            }

            //The link may have been created to a structure on an adjacent section
            KeyTracker<long> SourceLocationIDs = null;
            bool Success = PrimarySection.GetLocationsForStructure(structLinkObj.SourceID, out SourceLocationIDs);
            if (Success == false)
                return null;

            KeyTracker<long> TargetLocationIDs = null;
            Success = PrimarySection.GetLocationsForStructure(structLinkObj.TargetID, out TargetLocationIDs);
            if (Success == false)
                return null;

            ICollection<LocationCanvasView> SourceLocations = SourceLocationIDs.ValuesCopy().Select((l_id) => PrimarySection.GetLocation(l_id)).Where(l => l != null).ToArray();
            ICollection<LocationCanvasView> TargetLocations = TargetLocationIDs.ValuesCopy().Select((l_id) => PrimarySection.GetLocation(l_id)).Where(l => l != null).ToArray();

            SectionStructureLinkViewKey linkViewKey = SectionStructureLinkViewKey.CreateForNearestLocations(structLinkObj.ID, SourceLocations, TargetLocations);
            if (linkViewKey == null)
                return null;

            //OK, create a StructureLink between the locations
            return AnnotationViewFactory.Create(linkViewKey, PrimarySection.mapper);
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks()
        {
            return StructureLinks.Values;
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(GridRectangle bounds)
        {
            List<StructureLinkKey> intersectingIDs = StructureLinksSearch.Intersects(bounds.ToRTreeRect((float)this.PrimarySection.SectionNumber));
            return intersectingIDs.Select(id => StructureLinks[id]).ToList();
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(GridVector2 point)
        {
            List<StructureLinkKey> intersectingIDs = StructureLinksSearch.Intersects(point.ToRTreeRect((float)this.PrimarySection.SectionNumber));
            return intersectingIDs.Select(id => StructureLinks[id]).Where(sl => sl != null && sl.Contains(point)).ToList();
        }

        public ICollection<StructureLinkViewModelBase> GetStructureLinks(GridLineSegment line)
        {
            List<StructureLinkKey> intersectingIDs = StructureLinksSearch.Intersects(line.BoundingBox.ToRTreeRect((float)this.PrimarySection.SectionNumber));
            return intersectingIDs.Select(id => StructureLinks[id]).Where(sl => sl != null && sl.Intersects(line)).ToList();
        }
    }


}
