using System;
using System.Windows.Forms;

namespace Viking.UI
{
    public interface IPenState
    {
        /// <summary>
        /// Screen X coordinate
        /// </summary>
        int X { get; }

        /// <summary>
        /// Screen Y coordinate
        /// </summary>
        int Y { get; }

        /// <summary>
        /// How hard is the pen pushing on the surface? 0 if not supported.
        /// </summary>
        double NormalizedPressure { get; }

        /// <summary>
        /// Is the pen eraser active/touching
        /// </summary>
        bool Erase { get; }

        /// <summary>
        /// Is the pen upside down?
        /// </summary>
        bool Inverted { get; }

        /// <summary>
        /// Is the button on the pen barrel pressed?
        /// </summary>
        bool Barrel { get; }

        /// <summary>
        /// Is the pen touching the surface
        /// </summary>
        bool InContact { get; }

        /// <summary>
        /// Is the pen within range of the surface (not necessarily touching)
        /// </summary>
        bool InRange { get; }
    }

    public class PenEventArgs : EventArgs, IPenState
    {
        public System.Drawing.Point Location { get; internal set; }

        public int X { get { return Location.X; } }
        public int Y { get { return Location.Y; } }
        /// <summary>
        /// Raw data for our pen state
        /// </summary>
        public PointerPenInfo Pen { get; internal set; }

        public double NormalizedPressure
        {
            get
            {
                if ((Pen.mask & PenMask.Pressure) == 0)
                {
                    return 0;
                }

                return WinMsgInput.Config.NormalizeStylusPressure(Pen.pressure);
            }
        }

        public bool Erase { get { return (Pen.flags & PenFlags.Eraser) > 0; } }
        public bool Inverted { get { return (Pen.flags & PenFlags.Inverted) > 0; } }
        public bool Barrel { get { return (Pen.flags & PenFlags.Barrel) > 0; } }

        public bool InContact { get { return (Pen.pointerInfo.pointerFlags & PointerFlags.InContact) > 0; } }
        public bool InRange { get { return (Pen.pointerInfo.pointerFlags & PointerFlags.InRange) > 0; } }
    }

    public delegate void PenEventHandler(object sender, PenEventArgs e);

    public interface IPenEvents
    {
        /// <summary>
        /// The pen is close enough to the surface to be detected
        /// </summary>
        event PenEventHandler OnPenEnterRange;
        /// <summary>
        /// The pen is no longer close enough to the surface to be detected
        /// </summary>
        event PenEventHandler OnPenLeaveRange;
        /// <summary>
        /// The pen began touching the surface
        /// </summary>
        event PenEventHandler OnPenContact;
        /// <summary>
        /// The pen is no longer touching the surface
        /// </summary>
        event PenEventHandler OnPenLeaveContact;
        /// <summary>
        /// The pen has moved or the state of the pen has changed
        /// </summary>
        event PenEventHandler OnPenMove;
        /// <summary>
        /// A button on the pen has been depressed
        /// </summary>
        event PenEventHandler OnPenButtonDown;
        /// <summary>
        /// A button on the pen has been released
        /// </summary>
        event PenEventHandler OnPenButtonUp;
    }


    /// <summary>
    /// This class can be used by a control to support Pen Input Events
    /// </summary>
    public class PenEventManager : IPenEvents
    {
        public event PenEventHandler OnPenEnterRange;
        public event PenEventHandler OnPenLeaveRange;
        public event PenEventHandler OnPenContact;
        public event PenEventHandler OnPenLeaveContact;
        public event PenEventHandler OnPenMove;
        /// <summary>
        /// A button on the pen has been depressed
        /// </summary>
        public event PenEventHandler OnPenButtonDown;
        /// <summary>
        /// A button on the pen has been released
        /// </summary>
        public event PenEventHandler OnPenButtonUp;

        //It may appear tempting to add an erase event, but when erase is used the OS messages result in sending LeaveContact and Contact messages with the erase flag set on the latter.

        PointerPenInfo? previousPenState;
        PointerMessageData? previousPointerState;

        readonly System.Windows.Forms.Control Parent;

        public PenEventManager(Control parent)
        {
            Parent = parent;
        }

        /// <summary>
        /// This function must be called by the host controls WndProc function to process Pen related input events
        /// </summary>
        /// <param name="msg"></param>
        /// <returns>True if a pen message was processed</returns>
        public bool ProcessPenMessages(ref Message msg)
        {
            switch (msg.Msg)
            {
                case WinMsgInput.WM_TOUCHHITTESTING:
                    WinMsgInput.LogPenData(msg, "TouchHitTesting");
                    UpdatePenState(ref msg);
                    break;
                case WinMsgInput.WM_POINTERDEVICEINRANGE:
                    WinMsgInput.LogPenData(msg, "PointerDeviceInRange");
                    UpdatePenState(ref msg);
                    break;
                case WinMsgInput.WM_POINTERDEVICEOUTOFRANGE:
                    WinMsgInput.LogPenData(msg, "PointerDeviceOutOfRange");
                    UpdatePenState(ref msg);
                    break;
                case WinMsgInput.WM_POINTERUPDATE:
                    WinMsgInput.LogPenData(msg, "PointerUpdate");
                    UpdatePenState(ref msg);
                    break;
                case WinMsgInput.WM_POINTERDOWN:
                    WinMsgInput.LogPenData(msg, "PointerDown");
                    UpdatePenState(ref msg);
                    break;
                case WinMsgInput.WM_POINTERUP:
                    WinMsgInput.LogPenData(msg, "PointerUp");
                    UpdatePenState(ref msg);
                    break;
                default:

                    return false;
            }

            return true;
        }

        private void UpdatePenState(ref Message msg)
        {
            TouchMessageType msgType = (TouchMessageType)msg.Msg;
            PointerMessageData pointerState = new PointerMessageData(msg);
            WinMsgInput.GetPointerType(pointerState.PointerID, out PointerType type);
            WinMsgInput.IsPenEvent(out uint altID);
            //System.Diagnostics.Debug.Assert(altID == pointerState.PointerID); //WTF if this is wrong
            if (type != PointerType.Pen)
            {
                return;
            }

            PointerPenInfo penState = WinMsgInput.GetPenInfo(pointerState.PointerID);
            if(Global.TracePenEvents)
                System.Diagnostics.Trace.WriteLine($"{penState}");
            bool NewPointer = true; //True if we have a new pointer ID than last time.  From what I can tell each time the pen leaves range of the surface a new ID is assigned when moves back into range

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

            PenEventArgs args = new PenEventArgs
            {
                Location = Parent.PointToClient(new System.Drawing.Point(pointerState.X, pointerState.Y)),
                Pen = penState
            };

            if (pointerState.Flags.New)
            {
                FireOnPenEnterRange(args);
            }

            if (previousPointerState.HasValue)
            {
                PointerMessageData previousPointer = previousPointerState.Value;
                //Rather than writing a ton of if statements I xor the pointer flags.  Bits set to true in the result have changed
                /*var flagDelta = previousPointer.Flags.Flags ^ pointerState.Flags.Flags;
                PointerMessageFlags changed = new PointerMessageFlags(flagDelta);
                if (changed.InContact)
                {*/
                if (msgType == TouchMessageType.WM_POINTERUP && (previousPointer.Flags.InContact || pointerState.Flags.Up))
                {
                    FireOnPenLeaveContact(args);
                }
                else if (msgType == TouchMessageType.WM_POINTERDOWN && (!previousPointer.Flags.InContact && pointerState.Flags.InContact))
                {
                    FireOnPenContact(args);
                }
                else if (msgType == TouchMessageType.WM_POINTERUPDATE && previousPenState.Value.PositioningChange(penState))
                {
                    FireOnPenMove(args);
                } 
            }

            if (pointerState.Flags.InRange == false)
            {
                FireOnPenLeaveRange(args);
            }

            previousPenState = penState;
            previousPointerState = pointerState;
        }

        private void FireOnPenEnterRange(PenEventArgs e)
        {
            if (OnPenEnterRange != null)
            {
                Parent.BeginInvoke(OnPenEnterRange, Parent, e);
            }
        }

        private void FireOnPenLeaveRange(PenEventArgs e)
        {
            if (OnPenLeaveRange != null)
            {
                Parent.BeginInvoke(OnPenLeaveRange, Parent, e);
            }
        }

        private void FireOnPenContact(PenEventArgs e)
        {
            if (OnPenContact != null)
            {
                Parent.BeginInvoke(OnPenContact, Parent, e);
            }
        }
        private void FireOnPenLeaveContact(PenEventArgs e)
        {
            if (OnPenLeaveContact != null)
            {
                Parent.BeginInvoke(OnPenLeaveContact, Parent, e);
            }
        }
        private void FireOnPenMove(PenEventArgs e)
        {
            if (OnPenMove != null)
            {
                Parent.BeginInvoke(OnPenMove, Parent, e);
            }
        }

        private void FireOnPenButtonDown(PenEventArgs e)
        {
            if (OnPenButtonDown != null)
            {
                Parent.BeginInvoke(OnPenButtonDown, Parent, e);
            }
        }

        private void FireOnPenButtonUp(PenEventArgs e)
        {
            if (OnPenButtonUp != null)
            {
                Parent.BeginInvoke(OnPenButtonUp, Parent, e);
            }
        }

    }
}
