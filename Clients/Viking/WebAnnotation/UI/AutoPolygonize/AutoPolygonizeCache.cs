using Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// SAM2 viewport that produced a proposal. Shared by every cache entry from the same upload.
    /// </summary>
    internal readonly struct AutoPolygonizeUploadContext
    {
        public AutoPolygonizeUploadContext(double downsample, Rectangle worldBounds, int width, int height)
        {
            Downsample = downsample;
            WorldBounds = worldBounds;
            Width = width;
            Height = height;
        }

        public double Downsample { get; }
        public Rectangle WorldBounds { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>False when width/height were never recorded.</summary>
        public bool IsUsable => Width > 0 && Height > 0;

        /// <summary>
        /// True when this upload matches the live view: same resolved tile level and world bounds within 1%.
        /// </summary>
        public bool MatchesViewportForReuse(Rectangle liveBounds, double liveCameraDownsample)
        {
            if (!IsUsable)
                return false;

            return SegmentationViewportSession.StoredUploadViewMatches(
                WorldBounds,
                Downsample,
                liveBounds,
                liveCameraDownsample);
        }
    }

    /// <summary>
    /// Section-keyed skip/dismiss state plus the SAM2 viewport each proposal was produced from.
    /// Subscribes to <see cref="LocationObj"/> while the ID is cached so a geometry commit can
    /// drop the LastModified skip without losing the upload context.
    /// </summary>
    internal sealed class AutoPolygonizeCache
    {
        private readonly object gate = new();
        private readonly Dictionary<long, Entry> entries = [];
        private readonly Dictionary<int, HashSet<long>> idsBySection = [];

        /// <summary>Skip and overlay should drop; upload context is still on the entry. UI thread typical.</summary>
        public event Action<long, int, int>? GeometryInvalidated;

        /// <summary>Entry was fully removed (TypeCode, delete, ClearSection). Drop overlay if any.</summary>
        public event Action<long>? LocationForgotten;

        private sealed class Entry
        {
            public int SectionNumber;
            public LocationObj? Location;
            public DateTime? ProposalLastModified;
            public LocationType? ProposalTypeCode;
            /// <summary>
            /// Camera downsample when the last proposal or empty mask was remembered.
            /// Null until a completion records one. Not updated by a pending upload.
            /// </summary>
            public double? ProposalDownsample;
            public DateTime? DismissedAt;
            public AutoPolygonizeUploadContext? Upload;
            public int Generation;
        }

        /// <summary>
        /// False when the circle is not a circle, was dismissed at this LastModified, or already has a matching proposal
        /// at the same resolved tile downsample. Crossing the submit boundary (e.g. camera 1.0 → 1.21 maps to
        /// submitted 1 → 2) re-queues. Called by the idle batch.
        /// </summary>
        public bool ShouldProcess(
            long locationId,
            int sectionNumber,
            DateTime lastModified,
            LocationType typeCode,
            double liveDownsample = 0)
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
                    if (liveDownsample > 0 &&
                        entry.ProposalDownsample is double completed &&
                        completed > 0 &&
                        !SegmentationViewportSession.StoredTileLevelMatchesLiveCamera(completed, liveDownsample))
                    {
                        return true;
                    }

                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Records a published proposal so the same LastModified is not segmented again.
        /// Subscribes when <paramref name="location"/> is provided. Replaces the upload context when one is given.
        /// <paramref name="completedDownsample"/> is stored so a later camera change that
        /// resolves to a different tile pyramid level can re-queue. The current ceiling is 1.
        /// </summary>
        public void RememberProposal(
            long locationId,
            int sectionNumber,
            DateTime lastModified,
            LocationType typeCode,
            LocationObj? location = null,
            AutoPolygonizeUploadContext? upload = null,
            double completedDownsample = 0)
        {
            lock (gate)
            {
                Entry entry = GetOrAddEntryUnlocked(locationId, sectionNumber);
                entry.DismissedAt = null;
                entry.ProposalLastModified = lastModified;
                entry.ProposalTypeCode = typeCode;
                entry.ProposalDownsample = CompletedDownsample(completedDownsample, upload);
                AttachLocationUnlocked(entry, location);
                SetUploadUnlocked(entry, upload);
            }
        }

        /// <summary>
        /// Downsample to store on a completion. Prefers the caller's camera value, else the
        /// upload mosaic level. Always stores the resolved tile pyramid so zoom that
        /// changes that level compares cleanly. The current ceiling is 1.
        /// </summary>
        private static double? CompletedDownsample(double completedDownsample, AutoPolygonizeUploadContext? upload)
        {
            double raw = 0;
            if (completedDownsample > 0)
                raw = completedDownsample;
            else if (upload.HasValue && upload.Value.Downsample > 0)
                raw = upload.Value.Downsample;

            if (raw <= 0)
                return null;

            return SegmentationViewportSession.ResolveTileDownsample(raw);
        }

        /// <summary>
        /// Clears LastModified skips whose stored tile level differs from
        /// <paramref name="liveResolvedTileDownsample"/>. Called when the camera crosses
        /// the DS≤1 submit boundary so zoom-out and zoom-in both resegment.
        /// </summary>
        public void InvalidateCompletionsAtOtherTileDownsample(int liveResolvedTileDownsample)
        {
            lock (gate)
            {
                foreach (Entry entry in entries.Values)
                {
                    if (!entry.ProposalLastModified.HasValue)
                        continue;

                    if (entry.ProposalDownsample is not double completed || completed <= 0)
                    {
                        entry.ProposalLastModified = null;
                        entry.ProposalTypeCode = null;
                        entry.ProposalDownsample = null;
                        continue;
                    }

                    if ((int)completed != liveResolvedTileDownsample)
                    {
                        entry.ProposalLastModified = null;
                        entry.ProposalTypeCode = null;
                        entry.ProposalDownsample = null;
                    }
                }
            }
        }

        /// <summary>
        /// Suppresses the circle until LastModified changes. Keeps upload context and the PropertyChanged handler.
        /// Bumps <see cref="Entry.Generation"/> so a request already in flight for this circle (a resubmit or
        /// refresh running while the user rejects the ring) is discarded when it completes instead of
        /// republishing the ring and clearing the dismissal.
        /// </summary>
        public void Dismiss(long locationId, int sectionNumber, DateTime lastModified, LocationObj? location = null)
        {
            lock (gate)
            {
                Entry entry = GetOrAddEntryUnlocked(locationId, sectionNumber);
                entry.Generation++;
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
            lock (gate)
            {
                Entry entry = GetOrAddEntryUnlocked(locationId, sectionNumber);
                AttachLocationUnlocked(entry, location);
                SetUploadUnlocked(entry, upload);
                return entry.Generation;
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

        /// <summary>
        /// Drops skip, dismiss, subscription, and upload context. Used for delete, accept, and TypeCode.
        /// </summary>
        public void Remove(long locationId)
        {
            bool forgotten;
            lock (gate)
            {
                forgotten = RemoveEntryUnlocked(locationId);
            }

            if (forgotten)
                LocationForgotten?.Invoke(locationId);
        }

        /// <summary>Unsubscribes and drops every entry on that Z. Called when SectionAnnotationsView is evicted.</summary>
        public void ClearSection(int sectionNumber)
        {
            List<long> forgotten = [];
            lock (gate)
            {
                if (!idsBySection.TryGetValue(sectionNumber, out HashSet<long> ids))
                    return;

                foreach (long locationId in ids.ToArray())
                {
                    if (RemoveEntryUnlocked(locationId))
                        forgotten.Add(locationId);
                }
            }

            foreach (long locationId in forgotten)
                LocationForgotten?.Invoke(locationId);
        }

        public void Clear()
        {
            List<long> forgotten = [];
            lock (gate)
            {
                foreach (long locationId in entries.Keys.ToArray())
                {
                    if (RemoveEntryUnlocked(locationId))
                        forgotten.Add(locationId);
                }
            }

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
        /// Clears the LastModified skip so the next idle batch segments this circle again.
        /// Does not remove an on-screen proposal or a dismiss. Called when the mask overlay
        /// is turned on and the proposal was built without SAM2 mask bytes.
        /// </summary>
        public void InvalidateProposal(long locationId)
        {
            lock (gate)
            {
                if (!entries.TryGetValue(locationId, out Entry entry))
                    return;

                if (entry.DismissedAt.HasValue)
                    return;

                entry.ProposalLastModified = null;
                entry.ProposalTypeCode = null;
                entry.Generation++;
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

        private bool RemoveEntryUnlocked(long locationId)
        {
            if (!entries.TryGetValue(locationId, out Entry entry))
                return false;

            DetachLocationUnlocked(entry, entry.Location);
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

        private static void SetUploadUnlocked(Entry entry, AutoPolygonizeUploadContext? upload)
        {
            if (upload is { IsUsable: true } next)
                entry.Upload = next;
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
    }
}
