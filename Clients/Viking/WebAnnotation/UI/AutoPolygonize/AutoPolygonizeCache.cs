using Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// SAM2 viewport that produced a proposal. Shared by every cache entry from the same upload.
    /// </summary>
    internal readonly struct AutoPolygonizeUploadContext
    {
        public AutoPolygonizeUploadContext(ulong imageId, double downsample, GridRectangle worldBounds, int width, int height)
        {
            ImageId = imageId;
            Downsample = downsample;
            WorldBounds = worldBounds;
            Width = width;
            Height = height;
        }

        public ulong ImageId { get; }
        public double Downsample { get; }
        public GridRectangle WorldBounds { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>False when the session never recorded a usable server image.</summary>
        public bool IsUsable => ImageId != 0 && Width > 0 && Height > 0;
    }

    /// <summary>
    /// Section-keyed skip/dismiss state plus the SAM2 image lease used to produce each proposal.
    /// Subscribes to <see cref="LocationObj"/> while the ID is cached so a geometry commit can
    /// drop the LastModified skip without losing the upload context. DeleteImage runs only when
    /// the last hold on an image id is released.
    /// </summary>
    internal sealed class AutoPolygonizeCache
    {
        private readonly object gate = new();
        private readonly Dictionary<long, Entry> entries = [];
        private readonly Dictionary<int, HashSet<long>> idsBySection = [];
        private readonly Dictionary<ulong, int> imageHolds = [];

        /// <summary>Skip and overlay should drop; upload context is still on the entry. UI thread typical.</summary>
        public event Action<long, int, int>? GeometryInvalidated;

        /// <summary>Entry was fully removed (TypeCode, delete, ClearSection). Drop overlay if any.</summary>
        public event Action<long>? LocationForgotten;

        /// <summary>Last hold released. Controller should DeleteImage this id.</summary>
        public event Action<ulong>? ImageLeaseReleased;

        private sealed class Entry
        {
            public int SectionNumber;
            public LocationObj? Location;
            public DateTime? ProposalLastModified;
            public LocationType? ProposalTypeCode;
            public DateTime? DismissedAt;
            public AutoPolygonizeUploadContext? Upload;
            public int Generation;
        }

        /// <summary>
        /// False when the circle is not a circle, was dismissed at this LastModified, or already has a matching proposal.
        /// Pending-only entries (batch candidate) stay processable.
        /// </summary>
        public bool ShouldProcess(long locationId, int sectionNumber, DateTime lastModified, LocationType typeCode)
        {
            if (typeCode != LocationType.CIRCLE)
                return false;

            lock (gate)
            {
                if (!TryGetEntryUnlocked(locationId, sectionNumber, out Entry entry))
                    return true;

                if (entry.DismissedAt.HasValue && entry.DismissedAt.Value == lastModified)
                    return false;

                if (entry.ProposalLastModified.HasValue &&
                    entry.ProposalLastModified.Value == lastModified &&
                    entry.ProposalTypeCode == typeCode)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Records a published proposal so the same LastModified is not segmented again.
        /// Subscribes when <paramref name="location"/> is provided. Replaces the image lease when upload changes.
        /// </summary>
        public void RememberProposal(
            long locationId,
            int sectionNumber,
            DateTime lastModified,
            LocationType typeCode,
            LocationObj? location = null,
            AutoPolygonizeUploadContext? upload = null)
        {
            List<ulong> released = [];
            lock (gate)
            {
                Entry entry = GetOrAddEntryUnlocked(locationId, sectionNumber);
                entry.DismissedAt = null;
                entry.ProposalLastModified = lastModified;
                entry.ProposalTypeCode = typeCode;
                AttachLocationUnlocked(entry, location);
                SetUploadUnlocked(entry, upload, released);
            }

            RaiseReleased(released);
        }

        /// <summary>
        /// Suppresses the circle until LastModified changes. Keeps upload context and the PropertyChanged handler.
        /// </summary>
        public void Dismiss(long locationId, int sectionNumber, DateTime lastModified, LocationObj? location = null)
        {
            lock (gate)
            {
                Entry entry = GetOrAddEntryUnlocked(locationId, sectionNumber);
                entry.ProposalLastModified = null;
                entry.ProposalTypeCode = null;
                entry.DismissedAt = lastModified;
                AttachLocationUnlocked(entry, location);
            }
        }

        /// <summary>
        /// Subscribes and records the in-flight upload before SegmentImage so a mid-batch translate is heard.
        /// Does not set a LastModified skip.
        /// </summary>
        public int MarkPending(
            long locationId,
            int sectionNumber,
            LocationObj? location,
            AutoPolygonizeUploadContext? upload)
        {
            List<ulong> released = [];
            int generation;
            lock (gate)
            {
                Entry entry = GetOrAddEntryUnlocked(locationId, sectionNumber);
                AttachLocationUnlocked(entry, location);
                SetUploadUnlocked(entry, upload, released);
                generation = entry.Generation;
            }

            RaiseReleased(released);
            return generation;
        }

        public int GetGeneration(long locationId)
        {
            lock (gate)
            {
                return entries.TryGetValue(locationId, out Entry entry) ? entry.Generation : 0;
            }
        }

        public bool IsGenerationCurrent(long locationId, int generation)
        {
            lock (gate)
            {
                return entries.TryGetValue(locationId, out Entry entry) && entry.Generation == generation;
            }
        }

        public bool TryGetUploadContext(long locationId, out AutoPolygonizeUploadContext context)
        {
            lock (gate)
            {
                if (entries.TryGetValue(locationId, out Entry entry) && entry.Upload.HasValue)
                {
                    context = entry.Upload.Value;
                    return true;
                }
            }

            context = default;
            return false;
        }

        public bool Contains(long locationId)
        {
            lock (gate)
            {
                return entries.ContainsKey(locationId);
            }
        }

        public bool IsSubscribed(long locationId)
        {
            lock (gate)
            {
                return entries.TryGetValue(locationId, out Entry entry) && entry.Location is not null;
            }
        }

        /// <summary>Batch hold so FinishProcessBatch does not delete while entries still reference the id.</summary>
        public void AcquireBatchHold(ulong imageId)
        {
            if (imageId == 0)
                return;

            lock (gate)
            {
                AddHoldUnlocked(imageId);
            }
        }

        public void ReleaseBatchHold(ulong imageId)
        {
            List<ulong> released = [];
            lock (gate)
            {
                ReleaseHoldUnlocked(imageId, released);
            }

            RaiseReleased(released);
        }

        public bool IsImageHeld(ulong imageId)
        {
            lock (gate)
            {
                return imageHolds.TryGetValue(imageId, out int holds) && holds > 0;
            }
        }

        /// <summary>
        /// Drops skip, dismiss, subscription, and this entry's image hold. Used for delete, accept, and TypeCode.
        /// </summary>
        public void Remove(long locationId)
        {
            List<ulong> released = [];
            bool forgotten = false;
            lock (gate)
            {
                forgotten = RemoveEntryUnlocked(locationId, released);
            }

            RaiseReleased(released);
            if (forgotten)
                LocationForgotten?.Invoke(locationId);
        }

        /// <summary>Unsubscribes and releases every entry on that Z. Called when SectionAnnotationsView is evicted.</summary>
        public void ClearSection(int sectionNumber)
        {
            List<ulong> released = [];
            List<long> forgotten = [];
            lock (gate)
            {
                if (!idsBySection.TryGetValue(sectionNumber, out HashSet<long> ids))
                    return;

                foreach (long locationId in ids.ToArray())
                {
                    if (RemoveEntryUnlocked(locationId, released))
                        forgotten.Add(locationId);
                }
            }

            RaiseReleased(released);
            foreach (long locationId in forgotten)
                LocationForgotten?.Invoke(locationId);
        }

        public void Clear()
        {
            List<ulong> released = [];
            List<long> forgotten = [];
            lock (gate)
            {
                foreach (long locationId in entries.Keys.ToArray())
                {
                    if (RemoveEntryUnlocked(locationId, released))
                        forgotten.Add(locationId);
                }
            }

            RaiseReleased(released);
            foreach (long locationId in forgotten)
                LocationForgotten?.Invoke(locationId);
        }

        /// <summary>
        /// Store replace can swap the LocationObj instance. Retarget the handler when the ID is still cached.
        /// </summary>
        public void ReplaceLocation(LocationObj? oldLocation, LocationObj? newLocation)
        {
            if (newLocation is null)
                return;

            lock (gate)
            {
                if (!entries.TryGetValue(newLocation.ID, out Entry entry))
                    return;

                if (oldLocation is not null)
                    DetachLocationUnlocked(entry, oldLocation);

                if (newLocation.TypeCode != LocationType.CIRCLE)
                    return;

                AttachLocationUnlocked(entry, newLocation);
            }
        }

        /// <summary>
        /// Applies a PropertyChanged from a subscribed location. TypeCode leaving CIRCLE removes the entry;
        /// geometry names drop the LastModified skip and keep the upload context.
        /// </summary>
        public void ProcessPropertyChanged(long locationId, string? propertyName, LocationType typeCode)
        {
            if (typeCode != LocationType.CIRCLE)
            {
                if (AutoPolygonizeSelection.IsGeometryProperty(propertyName) ||
                    propertyName is nameof(LocationObj.TypeCode) or null or "")
                {
                    Remove(locationId);
                }

                return;
            }

            if (!AutoPolygonizeSelection.IsGeometryProperty(propertyName))
                return;

            int sectionNumber;
            int generation;
            lock (gate)
            {
                if (!entries.TryGetValue(locationId, out Entry entry))
                    return;

                entry.ProposalLastModified = null;
                entry.ProposalTypeCode = null;
                entry.DismissedAt = null;
                entry.Generation++;
                sectionNumber = entry.SectionNumber;
                generation = entry.Generation;
            }

            GeometryInvalidated?.Invoke(locationId, sectionNumber, generation);
        }

        private void OnLocationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not LocationObj location)
                return;

            ProcessPropertyChanged(location.ID, e.PropertyName, location.TypeCode);
        }

        private Entry GetOrAddEntryUnlocked(long locationId, int sectionNumber)
        {
            if (!entries.TryGetValue(locationId, out Entry entry))
            {
                entry = new Entry { SectionNumber = sectionNumber };
                entries[locationId] = entry;
                AddToSectionUnlocked(sectionNumber, locationId);
                return entry;
            }

            if (entry.SectionNumber != sectionNumber)
            {
                RemoveFromSectionUnlocked(entry.SectionNumber, locationId);
                entry.SectionNumber = sectionNumber;
                AddToSectionUnlocked(sectionNumber, locationId);
            }

            return entry;
        }

        private bool TryGetEntryUnlocked(long locationId, int sectionNumber, out Entry entry)
        {
            if (entries.TryGetValue(locationId, out entry))
                return entry.SectionNumber == sectionNumber;

            entry = null!;
            return false;
        }

        private bool RemoveEntryUnlocked(long locationId, List<ulong> released)
        {
            if (!entries.TryGetValue(locationId, out Entry entry))
                return false;

            DetachLocationUnlocked(entry, entry.Location);
            if (entry.Upload.HasValue)
                ReleaseHoldUnlocked(entry.Upload.Value.ImageId, released);

            entries.Remove(locationId);
            RemoveFromSectionUnlocked(entry.SectionNumber, locationId);
            return true;
        }

        private void AttachLocationUnlocked(Entry entry, LocationObj? location)
        {
            if (location is null)
                return;

            if (ReferenceEquals(entry.Location, location))
                return;

            DetachLocationUnlocked(entry, entry.Location);
            location.PropertyChanged += OnLocationPropertyChanged;
            entry.Location = location;
        }

        private void DetachLocationUnlocked(Entry entry, LocationObj? location)
        {
            if (location is null)
                return;

            location.PropertyChanged -= OnLocationPropertyChanged;
            if (ReferenceEquals(entry.Location, location))
                entry.Location = null;
        }

        private void SetUploadUnlocked(Entry entry, AutoPolygonizeUploadContext? upload, List<ulong> released)
        {
            if (!upload.HasValue || !upload.Value.IsUsable)
                return;

            AutoPolygonizeUploadContext next = upload.Value;
            if (entry.Upload.HasValue && entry.Upload.Value.ImageId == next.ImageId)
            {
                entry.Upload = next;
                return;
            }

            if (entry.Upload.HasValue)
                ReleaseHoldUnlocked(entry.Upload.Value.ImageId, released);

            entry.Upload = next;
            AddHoldUnlocked(next.ImageId);
        }

        private void AddHoldUnlocked(ulong imageId)
        {
            if (imageId == 0)
                return;

            imageHolds.TryGetValue(imageId, out int holds);
            imageHolds[imageId] = holds + 1;
        }

        private void ReleaseHoldUnlocked(ulong imageId, List<ulong> released)
        {
            if (imageId == 0 || !imageHolds.TryGetValue(imageId, out int holds))
                return;

            holds--;
            if (holds > 0)
            {
                imageHolds[imageId] = holds;
                return;
            }

            imageHolds.Remove(imageId);
            released.Add(imageId);
        }

        private void AddToSectionUnlocked(int sectionNumber, long locationId)
        {
            if (!idsBySection.TryGetValue(sectionNumber, out HashSet<long> ids))
            {
                ids = [];
                idsBySection[sectionNumber] = ids;
            }

            ids.Add(locationId);
        }

        private void RemoveFromSectionUnlocked(int sectionNumber, long locationId)
        {
            if (!idsBySection.TryGetValue(sectionNumber, out HashSet<long> ids))
                return;

            ids.Remove(locationId);
            if (ids.Count == 0)
                idsBySection.Remove(sectionNumber);
        }

        private void RaiseReleased(List<ulong> released)
        {
            foreach (ulong imageId in released)
                ImageLeaseReleased?.Invoke(imageId);
        }
    }
}
