using System;
using System.Windows.Forms;

namespace Viking.UI
{
    public interface IGestureEvents
    {
        event PanGestureEventHandler OnGesturePan;
        event ZoomGestureEventHandler OnGestureZoom;
        event BeginGestureEventHandler OnGestureBegin;
        event EndGestureEventHandler OnGestureEnd;
    }

    public delegate void BeginGestureEventHandler(object sender, BeginGestureEventArgs e);
    public delegate void EndGestureEventHandler(object sender, GestureEventArgs e);
    public delegate void PanGestureEventHandler(object sender, PanGestureEventArgs e);
    public delegate void ZoomGestureEventHandler(object sender, PanGestureEventArgs e);


    public class GestureEventArgs : EventArgs
    { 
        public Gesture Type { get { return Gesture.Gesture; } }

        /// <summary>
        /// Raw data for our pen state
        /// </summary>
        public GestureInfo Gesture { get; internal set; }
    }

    public class BeginGestureEventArgs : GestureEventArgs
    {
        /// <summary>
        /// Position when BEGIN message was recieved
        /// </summary>
        public System.Drawing.Point BeginPt { get; internal set; } 
    }


    public class ZoomGestureEventArgs : GestureEventArgs
    {
        /// <summary>
        /// Position when BEGIN message was recieved
        /// </summary>
        public System.Drawing.Point BeginPt { get; internal set; }

        /// <summary>
        /// Position at latest update
        /// </summary>
        public System.Drawing.Point EndPt { get; internal set; }

        public System.Drawing.Point Delta { 
            get
            {
                System.Drawing.Point p = new System.Drawing.Point
                {
                    X = EndPt.X - BeginPt.X,
                    Y = EndPt.Y - BeginPt.Y
                };
                return p;
            }
        }
    }

    public class PanGestureEventArgs : GestureEventArgs
    {
        /// <summary>
        /// Position when BEGIN message was recieved
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

        public override string ToString()
        {
            return $"B:{BeginPt} E:{EndPt} D:{Delta}";
        }
    }


    class GestureEventManager : IGestureEvents
    {
        public event PanGestureEventHandler OnGesturePan;
        public event ZoomGestureEventHandler OnGestureZoom;
        public event BeginGestureEventHandler OnGestureBegin;
        public event EndGestureEventHandler OnGestureEnd;

        GestureInfo? PreviousGestureInfo;

        readonly System.Windows.Forms.Control Parent;

        System.Drawing.Point BeginPosition;

        System.Drawing.Point LastGesturePositon;

        public GestureEventManager(System.Windows.Forms.Control parent)
        {
            Parent = parent;
        }

        /// <summary>
        /// This function must be called by the host controls WndProc function to process Pen related input events
        /// </summary>
        /// <param name="msg"></param>
        /// <returns>True if a pen message was processed</returns>
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
            return false;
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

                        GestureEventArgs e = new GestureEventArgs() { Gesture = newInfo};
                        FireOnGestureEnd(e);
                        break;
                    }
                case Gesture.Begin:
                    {
                        var point = Parent.PointToClient(new System.Drawing.Point(newInfo.Location.x, newInfo.Location.y));
                        BeginPosition = point;
                        LastGesturePositon = point;
                        var e = new BeginGestureEventArgs() { Gesture = newInfo, BeginPt = point };
                        PreviousGestureInfo = newInfo;
                        FireOnGestureBegin(e);
                        break;
                    }
                case Gesture.Pan:
                    {
                        var point = Parent.PointToClient(new System.Drawing.Point(newInfo.Location.x, newInfo.Location.y));
                        var e = new PanGestureEventArgs()
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
                        var e = new PanGestureEventArgs()
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
