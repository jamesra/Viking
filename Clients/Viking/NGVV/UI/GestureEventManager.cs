using System;
using System.Windows.Forms;

namespace Viking.UI
{
    /// <summary>
    /// Touch gesture notifications raised by a control that hosts a <see cref="GestureEventManager"/>.
    /// Handlers run on the host control's UI thread.
    /// </summary>
    public interface IGestureEvents
    {
        /// <summary>Raised for each pan update of a gesture in progress.</summary>
        event PanGestureEventHandler OnGesturePan;
        /// <summary>Raised for each zoom update; zoom reuses <see cref="PanGestureEventArgs"/>.</summary>
        event ZoomGestureEventHandler OnGestureZoom;
        /// <summary>Raised when a gesture starts.</summary>
        event BeginGestureEventHandler OnGestureBegin;
        /// <summary>Raised when a gesture finishes.</summary>
        event EndGestureEventHandler OnGestureEnd;
    }


    /// <summary>Handler for <see cref="IGestureEvents.OnGestureBegin"/>.</summary>
    public delegate void BeginGestureEventHandler(object sender, BeginGestureEventArgs e);
    /// <summary>Handler for <see cref="IGestureEvents.OnGestureEnd"/>.</summary>
    public delegate void EndGestureEventHandler(object sender, GestureEventArgs e);
    /// <summary>Handler for <see cref="IGestureEvents.OnGesturePan"/>.</summary>
    public delegate void PanGestureEventHandler(object sender, PanGestureEventArgs e);
    /// <summary>Handler for <see cref="IGestureEvents.OnGestureZoom"/>; receives <see cref="PanGestureEventArgs"/>.</summary>
    public delegate void ZoomGestureEventHandler(object sender, PanGestureEventArgs e);


    /// <summary>Base event data for a Windows WM_GESTURE message.</summary>
    public class GestureEventArgs : EventArgs
    {
        /// <summary>The gesture kind reported by <see cref="Gesture"/> (begin, end, pan, zoom, ...).</summary>
        public Gesture Type => Gesture.Gesture;

        /// <summary>
        /// Raw data for our pen state
        /// </summary>
        public GestureInfo Gesture { get; internal set; }
    }

    /// <summary>Event data raised when a gesture begins.</summary>
    public class BeginGestureEventArgs : GestureEventArgs
    {
        /// <summary>
        /// Position when BEGIN message was received
        /// </summary>
        public System.Drawing.Point BeginPt { get; internal set; }
    }


    /// <summary>Event data carrying begin/end points of a zoom gesture. Currently unused by <see cref="GestureEventManager"/>, which raises zoom with <see cref="PanGestureEventArgs"/>.</summary>
    public class ZoomGestureEventArgs : GestureEventArgs
    {
        /// <summary>
        /// Position when BEGIN message was received
        /// </summary>
        public System.Drawing.Point BeginPt { get; internal set; }

        /// <summary>
        /// Position at latest update
        /// </summary>
        public System.Drawing.Point EndPt { get; internal set; }

        /// <summary>Offset from <see cref="BeginPt"/> to <see cref="EndPt"/>.</summary>
        public System.Drawing.Point Delta
        {
            get
            {
                System.Drawing.Point p = new()
                {
                    X = EndPt.X - BeginPt.X,
                    Y = EndPt.Y - BeginPt.Y
                };
                return p;
            }
        }
    }

    /// <summary>Event data for pan updates, also used for zoom updates.</summary>
    public class PanGestureEventArgs : GestureEventArgs
    {
        /// <summary>
        /// Position when BEGIN message was received
        /// </summary>
        public System.Drawing.Point BeginPt { get; internal set; }

        /// <summary>
        /// Position at latest update
        /// </summary>
        public System.Drawing.Point EndPt { get; internal set; }

        /// <summary>
        /// Distance travelled since the last gesture message
        /// </summary>
        public System.Drawing.Point Delta { get; internal set; }

        /// <summary>Formats begin point, end point and delta for diagnostics.</summary>
        public override string ToString() => $"B:{BeginPt} E:{EndPt} D:{Delta}";
    }


    /// <summary>
    /// Translates WM_GESTURE messages from a host control into <see cref="IGestureEvents"/> events.
    /// </summary>
    /// <remarks>
    /// Created by <c>SectionViewerControl</c>, which forwards its WndProc messages to
    /// <see cref="ProcessGestureMessages"/>. Events are marshalled with <c>Control.BeginInvoke</c> to the
    /// parent's UI thread, so handlers never run inside WndProc.
    /// </remarks>
    class GestureEventManager(System.Windows.Forms.Control parent) : IGestureEvents
    {
        /// <inheritdoc/>
        public event PanGestureEventHandler OnGesturePan;
        /// <inheritdoc/>
        public event ZoomGestureEventHandler OnGestureZoom;
        /// <inheritdoc/>
        public event BeginGestureEventHandler OnGestureBegin;
        /// <inheritdoc/>
        public event EndGestureEventHandler OnGestureEnd;

        GestureInfo? PreviousGestureInfo;

        readonly System.Windows.Forms.Control Parent = parent;

        System.Drawing.Point BeginPosition;

        System.Drawing.Point LastGesturePositon;

        /// <summary>
        /// This function must be called by the host controls WndProc function to process touch gesture (WM_GESTURE) messages
        /// </summary>
        /// <param name="msg">The window message from the host control's WndProc.</param>
        /// <returns>True if a gesture message was recognised and an event was queued</returns>
        public bool ProcessGestureMessages(ref Message msg)
        {
            switch (msg.Msg)
            {
                case WinMsgInput.WM_GESTURE:
                    //WinMsgInput.LogPenData(msg, "Gesture");
                    bool handled = UpdateGestureState(ref msg);
                    return handled;
                default:

                    return false;
            }
        }

        private bool UpdateGestureState(ref Message msg)
        {
            bool InfoFound = GestureSupport.ProcessGestureMessage(ref msg, out GestureInfo newInfo);
            if (InfoFound == false)
                return false;

            //Trace.WriteLine($"{newInfo}");
            //bool NewGesture = true; //True if we have a new pointer ID than last time.  From what I can tell each time the pen leaves range of the surface a new ID is assigned when moves back into range

            /*
            //Reset our previous state if the ID has changed
            if (previousPointerState.HasValue)
            {
                NewPointer = previousPointerState.Value.PointerID != pointerState.PointerID;
                if (NewPointer)
                {
                    previousPointerState = new PointerMessageData?();
                    previousPenState = new PointerPenInfo?();
                }
            }
            */

            switch (newInfo.Gesture)
            {
                case Gesture.End:
                    {
                        PreviousGestureInfo = null;

                        GestureEventArgs e = new() { Gesture = newInfo };
                        FireOnGestureEnd(e);
                        break;
                    }
                case Gesture.Begin:
                    {
                        var point = Parent.PointToClient(new System.Drawing.Point(newInfo.Location.x, newInfo.Location.y));
                        BeginPosition = point;
                        LastGesturePositon = point;
                        BeginGestureEventArgs e = new() { Gesture = newInfo, BeginPt = point };
                        PreviousGestureInfo = newInfo;
                        FireOnGestureBegin(e);
                        break;
                    }
                case Gesture.Pan:
                    {
                        var point = Parent.PointToClient(new System.Drawing.Point(newInfo.Location.x, newInfo.Location.y));
                        PanGestureEventArgs e = new()
                        {
                            Gesture = newInfo,
                            BeginPt = BeginPosition,
                            EndPt = point,
                            Delta = new System.Drawing.Point(newInfo.Location.x - LastGesturePositon.X, newInfo.Location.y - LastGesturePositon.Y)
                        };
                        LastGesturePositon = newInfo.Location;
                        FireOnPanGesture(e);
                        break;
                    }
                case Gesture.Zoom:
                    {
                        var point = Parent.PointToClient(new System.Drawing.Point(newInfo.Location.x, newInfo.Location.y));
                        //var e = new ZoomGestureEventArgs() { Gesture = newInfo, BeginPt = BeginPosition, EndPt = point };
                        PanGestureEventArgs e = new()
                        {
                            Gesture = newInfo,
                            BeginPt = BeginPosition,
                            EndPt = point,
                            Delta = new System.Drawing.Point(newInfo.Location.x - LastGesturePositon.X, newInfo.Location.y - LastGesturePositon.Y)
                        };
                        LastGesturePositon = newInfo.Location;
                        FireOnZoomGesture(e);
                        break;
                    }
                default:
                    return false;
            }

            /*
            previousPenState = penState;
            previousPointerState = pointerState;
            */

            return true;
        }

        private void FireOnPanGesture(PanGestureEventArgs e)
        {
            if (OnGesturePan != null)
            {
                Parent.BeginInvoke(OnGesturePan, Parent, e);
            }
        }
        private void FireOnZoomGesture(PanGestureEventArgs e)
        {
            if (OnGestureZoom != null)
            {
                Parent.BeginInvoke(OnGestureZoom, Parent, e);
            }
        }
        private void FireOnGestureBegin(BeginGestureEventArgs e)
        {
            if (OnGestureBegin != null)
            {
                Parent.BeginInvoke(OnGestureBegin, Parent, e);
            }
        }

        private void FireOnGestureEnd(GestureEventArgs e)
        {
            if (OnGestureEnd != null)
            {
                Parent.BeginInvoke(OnGestureEnd, Parent, e);
            }
        }
    }
}
