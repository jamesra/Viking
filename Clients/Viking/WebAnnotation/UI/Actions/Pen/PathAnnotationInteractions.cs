using Geometry;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    public enum AnnotationRegionInteraction
    {
        /// <summary>
        /// The path entered a region
        /// </summary>
        ENTER,
        /// <summary>
        /// The path exited a region
        /// </summary>
        EXIT,
        /// <summary>
        /// The pen made contact with the surface 
        /// </summary>
        PENDOWN,
        /// <summary>
        /// The pen lost contact with the surface
        /// </summary>
        PENUP,
        /// <summary>
        /// A loop was formed in the path
        /// </summary>
        LOOP
    }

    public class LocationInteractionLogEvent : InteractionLogEvent
    {
        public readonly LocationObj location = null;
        public LocationInteractionLogEvent(InteractionLogEvent e) : this(e.Interaction, e.Annotation, e.Index)
        {
        }

        public LocationInteractionLogEvent(AnnotationRegionInteraction interaction, ICanvasView annotation, int index) : base(interaction, annotation, index)
        {
            IViewLocation loc = annotation as IViewLocation;
            if (loc == null)
                return;

            location = Store.Locations.GetObjectByID(loc.ID);
        }

        public override string ToString()
        {
            return string.Format("{0} {1} @ {2}", Interaction, Annotation == null ? "null" : Annotation.ToString(), Index);
        }

        public static LocationInteractionLogEvent[] CreateFromLog(IReadOnlyList<InteractionLogEvent> log_entries)
        {
            return log_entries.Select(e => new LocationInteractionLogEvent(e)).ToArray();
        }
    }

    public class InteractionLogEvent : IEquatable<InteractionLogEvent>
    {
        public readonly AnnotationRegionInteraction Interaction;

        /// <summary>
        /// Hit-testing item that we intersected.  Can be null if path moved to a region with no annotations
        /// </summary>
        public readonly ICanvasView Annotation;

        /// <summary>
        /// Index into the path/polyline where this event occured.
        /// </summary>
        public readonly int Index;

        public InteractionLogEvent(AnnotationRegionInteraction interaction, ICanvasView annotation, int index)
        {
            Interaction = interaction;
            Annotation = annotation;
            Index = index;
        }

        public bool Equals(InteractionLogEvent other)
        {
            if (this.Index != other.Index)
                return false;

            if (this.Interaction != other.Interaction)
                return false;

            if (object.ReferenceEquals(this.Annotation, other.Annotation))
                return true;

            if (this.Annotation == other.Annotation)
            {
                return true;
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            InteractionLogEvent e = obj as InteractionLogEvent;
            if (e == null)
                return false;

            return this.Equals(e);
        }

        public override int GetHashCode()
        {
            return Index;
        }

        public override string ToString()
        {
            return string.Format("{0} {1} @ {2}", Interaction, Annotation == null ? "null" : Annotation.ToString(), Index);
        }
    }

    /// <summary>
    /// A simplified list of states ordered in time that include what, where, and when a path intersected annotations.
    /// Some annotations can overlap, for example adjacent annotations rendered on top of the annotation on the current section.
    /// 
    /// </summary>
    public class PathAnnotationInteractionLog : INotifyCollectionChanged
    {
        /// Here is prototype output for a path drawn from one cell, to another cell, back out, crossing itself in empty space, and returning to the original cell:
        /// 0	PENDOWN	A	0
        ///1	LEAVE A   34
        ///2	ENTER NULL    34
        ///3	LEAVE NULL    54
        ///4	ENTER B   54
        ///5	ENTER B(Adj Link)    85
        ///6	LEAVE B(Adj Link)    96
        ///7	LEAVE B   123
        ///8	ENTER NULL    123
        ///9	LEAVE NULL    134
        //10	LOOP		154
        //11	ENTER A   186
        //12	LOOP		193
        //13	PENUP A   203

        public IReadOnlyList<InteractionLogEvent> Entries
        {
            get
            {
                return _Entries;
            }

        }

        List<InteractionLogEvent> _Entries = new List<InteractionLogEvent>();

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnLogChanged;
        event NotifyCollectionChangedEventHandler System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged
        {
            add
            {
                this.OnLogChanged += value;
            }
            remove
            {
                this.OnLogChanged -= value;
            }
        }

        public void FireOnLogChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (OnLogChanged != null)
                OnLogChanged(sender, e);
        }

        public PathAnnotationInteractionLog()
        {
        }

        public InteractionLogEvent Last
        {
            get
            {
                if (_Entries.Count == 0)
                    return null;

                return _Entries[_Entries.Count - 1];
            }
        }

        /// <summary>
        /// Return the most recent annotation in the log
        /// </summary>
        public ICanvasView LastEventAnnotation
        {
            get
            {
                if (_Entries.Count == 0)
                    return null;

                return _Entries[_Entries.Count - 1].Annotation;
            }
        }

        public bool Empty { get { return _Entries.Count == 0; } }

        public void Add(InteractionLogEvent entry)
        {
            Trace.WriteLine(string.Format("Add {0}", entry));
            _Entries.Add(entry);

            FireOnLogChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, entry));
        }
        /*
        public void RemoveEntry(InteractionLogEvent entry)
        {
            Log.Remove(entry);
        }*/

        public InteractionLogEvent Pop()
        {
            var entry = _Entries.Last();
            _Entries.RemoveAt(_Entries.Count - 1);

            FireOnLogChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, entry));

            return entry;
        }

        public void Clear()
        {
            _Entries.Clear();
            FireOnLogChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public static class PathInteractionExtensions
    {
        /// <summary>
        /// Returns the path between two log entries, inclusive
        /// </summary>
        /// <param name="Start"></param>
        /// <param name="Finish"></param>
        /// <returns></returns>
        public static GridVector2[] PathBetween(this IReadOnlyList<GridVector2> path, InteractionLogEvent Start, InteractionLogEvent Finish)
        {
            return PathBetween(path, Start.Index, Finish.Index);
        }

        public static GridVector2[] PathBetween(this IReadOnlyList<GridVector2> path, int Start, int Finish)
        {
            int length = (Finish - Start) + 1; //Inclusive, so add 1

            Debug.Assert(length > 1, "PathBetween request is inclusive and should have a length greater than 1");

            GridVector2[] output = new GridVector2[length];

            for (int i = Start; i < Finish + 1; i++)
            {
                output[i - Start] = path[i];
            }

            return output;
        }
    }


    /// <summary>
    /// Listens to changes to a path object and generates a log of interaction events
    /// </summary>
    internal class PathInteractionLogger
    {
        /// Here is prototype output for a path drawn from one cell, to another cell, back out, crossing itself in empty space, and returning to the original cell:
        /// 0	PENDOWN	A	0
        ///1	LEAVE A   34
        ///2	ENTER NULL    34
        ///3	LEAVE NULL    54
        ///4	ENTER B   54
        ///5	ENTER B(Adj Link)    85
        ///6	LEAVE B(Adj Link)    96
        ///7	LEAVE B   123
        ///8	ENTER NULL    123
        ///9	LEAVE NULL    134
        //10	LOOP		154
        //11	ENTER A   186
        //12	LOOP		193
        //13	PENUP A   203 

        private readonly Path _Path;
        private readonly ICanvasViewHitTesting Overlay;
        public PathAnnotationInteractionLog Log;

        /// <summary>
        /// The annotations the tip of the path was over the last time we checked
        /// </summary>
        private List<ICanvasView> CurrentlyIntersected = new List<ICanvasView>();

        public PathInteractionLogger(Path path, ICanvasViewHitTesting overlay, PathAnnotationInteractionLog log)
        {
            Log = log;
            _Path = path;
            Overlay = overlay;

            path.OnPathChanged += this.OnPathChanged;
            path.OnLoopChanged += this.OnLoopChanged;
        }

        public PathInteractionLogger(Path path, ICanvasViewHitTesting overlay) : this(path, overlay, new PathAnnotationInteractionLog())
        {
        }

        private void OnPathChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddLogEntriesForNewSegment();
                    break;
                case NotifyCollectionChangedAction.Move:
                    throw new NotImplementedException("Move is not implemented for PathInteractionLogger");
                case NotifyCollectionChangedAction.Remove:
                    RemoveLogEntriesAfterErase();
                    break;
                case NotifyCollectionChangedAction.Replace:
                    throw new NotImplementedException("Replace is not implemented for PathInteractionLogger");
                case NotifyCollectionChangedAction.Reset:
                    Log.Clear();
                    break;
            }
        }

        /// <summary>
        /// As we draw a line from A to B we need to know which canvas entities we've crossed
        /// 
        /// A ----------------------------------------- B
        /// 
        /// -----0             1-----1a---1a-----1 2--------------------2
        ///
        /// Where 1a represents a contained annotation with annotation 1. Such as an interior hole
        /// or location link. 
        /// 
        /// In the above case we'd expect log entries:
        ///     Exit 0
        ///     Enter 1
        ///     Exit 1
        ///     Enter 1a
        ///     Exit 1a
        ///     Enter 1
        ///     Exit 1
        ///     Enter 2
        ///     
        /// </summary>
        void AddLogEntriesForNewSegment()
        {
            HitTestResult[] candidates = null;
            HitTestResult[] line_intersect_candidates = null; //Canvas entities that intersect the most recent line
            HitTestResult[] point_intersect_candidates = null; //Canvas entities that intersect the most recent point

            ICanvasView[] point_intersections = null;

            if (_Path.HasSegment)
            {
                /// We have to check both the line and endpoint to 
                /// Checking only the new endpoints would result in
                ///     Exit 0
                ///     Enter 2 

                GridLineSegment latest = _Path.NewestSegment;
                candidates = Overlay.GetAnnotations(latest.BoundingBox).ToArray();

                point_intersect_candidates = candidates.Where(o => o.obj.Contains(latest.A)).ToArray();
                point_intersections = point_intersect_candidates.Select(o => (ICanvasView)o.obj).ToArray();

                line_intersect_candidates = candidates.Where(o => ((ICanvasView)o.obj).Intersects(latest)).ToArray();
                //                .Where(o => o.obj.Intersects(latest) || o.obj.Contains(latest.A)).ToArray();
            }
            else
            {
                AddLogEntryForFirstPoint();
                return;
            }

            //point_intersect_candidates = point_intersect_candidates.ExpandICanvasViewContainers(_Path.Points[0]).ToArray();
            HitTestResult[] expanded_point_intersect_candidates = point_intersect_candidates.ExpandICanvasViewContainers(_Path.Points[_Path.Points.Count - 1]).Where(c => c != null).ToArray();
            ICanvasView[] expanded_point_intersections = expanded_point_intersect_candidates.Select(o => (ICanvasView)o.obj).ToArray();
            //Contains for LocationPolygonView is semi-broken because we need to select holes in the polygon for UI purposes.  However for pen
            //purposes we want contains to return false.  The workaround is that if the point is inside the interior hole it has a distance > 1
            //where any other annotation that returns contains == true would have a distance == 0
            List<ICanvasView> new_line_intersections = expanded_point_intersect_candidates.Select(c => c.obj as ICanvasView).ToList();
            //List<ICanvasView> new_point_intersections = point_intersect_candidates.Select(c => c.obj as ICanvasView).ToList();

            /////////Handle Exit cases first (So they appear before Enter entries) ////////////////////
            foreach (var previous_intersection in this.CurrentlyIntersected)
            {
                if (expanded_point_intersections.Contains(previous_intersection))
                    continue; //No change in intersection status

                var new_event = new InteractionLogEvent(AnnotationRegionInteraction.EXIT, previous_intersection, _Path.Points.Count - 1);
                Log.Add(new_event);
            }

            //Check if we need to log enter/exit of unannotated space
            if (new_line_intersections.Count > 0 && CurrentlyIntersected.Count == 0)
            {
                var new_event = new InteractionLogEvent(AnnotationRegionInteraction.EXIT, null, _Path.Points.Count - 1);
                Log.Add(new_event);
            }
            else if (new_line_intersections.Count == 0 && CurrentlyIntersected.Count > 0)
            {
                var new_event = new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, _Path.Points.Count - 1);
                Log.Add(new_event);
            }
            else if (new_line_intersections.Count == 0 && CurrentlyIntersected.Count == 0 && Log.Entries.Count == 0)
            {
                //Check the case of putting the pen down for the first time in empty space
                var new_event = new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, _Path.Points.Count - 1);
                Log.Add(new_event);
            }

            /////////Handle ENTER cases second (So they appear after Exit entries) ////////////////////

            foreach (ICanvasView candidate in expanded_point_intersections)
            {
                if (CurrentlyIntersected.Contains(candidate))
                    continue;

                var new_event = new InteractionLogEvent(AnnotationRegionInteraction.ENTER, candidate, _Path.Points.Count - 1);
                Log.Add(new_event);
            }

            CurrentlyIntersected = new_line_intersections;
        }

        private void AddLogEntryForFirstPoint()
        {
            var candidates = Overlay.GetAnnotations(_Path.Points[0]).ToArray();
            var point_intersect_candidates = candidates.Where(o => o.obj.Contains(_Path.Points[0])).ToArray();
            point_intersect_candidates = point_intersect_candidates.ExpandICanvasViewContainers(_Path.Points[0]).ToArray();

            if (point_intersect_candidates.Length > 0)
            {
                foreach (var first_touch_entity in point_intersect_candidates)
                {
                    if (CurrentlyIntersected.Contains(first_touch_entity.obj))
                        continue;

                    var new_event = new InteractionLogEvent(AnnotationRegionInteraction.ENTER, (ICanvasView)first_touch_entity.obj, _Path.Points.Count - 1);
                    Log.Add(new_event);
                }
            }
            else
            {
                var new_event = new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, _Path.Points.Count - 1);
                Log.Add(new_event);
            }

            CurrentlyIntersected = point_intersect_candidates.Select(c => c.obj as ICanvasView).ToList();
        }

        /// <summary>
        /// Removes all entries with an index higher than the path length
        /// </summary>
        void RemoveLogEntriesAfterErase()
        {
            int max_index = _Path.Points.Count - 1;
            while (Log.Last.Index > max_index && Log.Empty == false)
            {
                Log.Pop();
            }

            //Update the current intersection list
            GridLineSegment latest = _Path.NewestSegment;
            var candidates = Overlay.GetAnnotations(latest.BoundingBox).Where(o => ((ICanvasView)o.obj).Intersects(latest) || o.obj.Contains(latest.A)).ToArray();
            List<ICanvasView> newIntersections = candidates.Select(c => c.obj as ICanvasView).ToList();
            this.CurrentlyIntersected = newIntersections;
        }

        private void OnLoopChanged(object sender, bool has_loop)
        {
            if (has_loop)
            {
                InteractionLogEvent entry = new InteractionLogEvent(AnnotationRegionInteraction.LOOP, Log.LastEventAnnotation, _Path.Points.Count);
                Log.Add(entry);
            }
        }
    }
}
