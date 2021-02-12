using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Viking.UI
{
    [Flags]
    public enum TouchHitTesting
    {
        Default = 0, //WM_TOUCHHITTESTING messages are not sent to the target window but are sent to child windows.
        Client =  1, //messages are sent to the target window.
        None   =  2 //messages are not sent to the target window or child windows.
    };

    [Flags]
    public enum TouchRegisterOptions
    {
        None =      0,
        FineTouch = 0b0001,
        WantPalm  = 0b0010
    }

    
    public enum PointerType : System.Int32
    {
        /// <summary>
        /// Generic pointer type. This type never appears in pointer messages or pointer data. Some data query functions allow the caller to restrict the query to specific pointer type. The PT_POINTER type can be used in these functions to specify that the query is to include pointers of all types
        /// </summary>
        Pointer = 0x00000001,
        Touch = 0x00000002,
        Pen = 0x00000003,
        Mouse = 0x00000004,
        Touchpad = 0x05
    }

    [Flags]
    public enum PenFlags : System.UInt32
    {
        None = 0,
        /// <summary>
        /// The barrel button is pressed
        /// </summary>
        Barrel   = 0b0001,
        /// <summary>
        /// The pen is inverted
        /// </summary>
        Inverted = 0b0010,
        /// <summary>
        /// The eraser button is pressed
        /// </summary>
        Eraser   = 0b0100
    }


    [Flags]
    public enum PointerFlags : System.UInt32
    {
        /// <summary>
        /// No flags
        /// </summary>
        None = 0,
        /// <summary>
        /// Indicates the arrival of a new pointer.
        /// </summary>
        New = 1,
        /// <summary>
        /// Indicates that this pointer continues to exist. When this flag is not set, it indicates the pointer has left detection range.
        /// </summary>
        InRange = 0x02,
        /// <summary>
        /// Indicates that this pointer is in contact with the digitizer surface.
        /// </summary>
        InContact =    0x04, 
        FirstButton =  0x10,
        SecondButton = 0x20,
        ThirdButton =  0x40,
        FourthButton = 0x80,
        FifthButton = 0x100,
        /// <summary>
        /// Indicates that this pointer has been designated as the primary pointer.
        /// </summary>
        Primary =    0x2000,
        /// <summary>
        /// Confidence is a suggestion from the source device about whether the pointer represents an intended or accidental interaction
        /// </summary>
        Confidence = 0x4000,
        /// <summary>
        /// Indicates that the pointer is departing in an abnormal manner, such as when the system receives invalid input for the pointer or when a device with active pointers departs abruptly.
        /// </summary>
        Cancelled =  0x8000,
        /// <summary>
        /// Indicates that this pointer transitioned to a down state; that is, it made contact with the digitizer surface.
        /// </summary>
        Down =      0x10000,
        /// <summary>
        /// Indicates that this is a simple update that does not include pointer state changes.
        /// </summary>
        Update =    0x20000,
        /// <summary>
        /// Indicates that this pointer transitioned to an up state; that is, contact with the digitizer surface ended.
        /// </summary>
        Up =        0x40000,
        /// <summary>
        /// Indicates input associated with a pointer wheel. 
        /// </summary>
        Wheel =     0x80000,
        /// <summary>
        /// Indicates input associated with a pointer h-wheel.
        /// </summary>
        HWheel =   0x100000,
        /// <summary>
        /// Indicates that this pointer was captured by (associated with) another element and the original element has lost capture
        /// </summary>
        CaptureChanged = 0x200000,
        /// <summary>
        /// Indicates that this pointer has an associated transform.
        /// </summary>
        HasTransform = 0x400000
    }

    [Flags]
    public enum PenMask : System.UInt32
    {
        None =     0x0,
        Pressure = 0b0001,
        Rotation = 0b0010,
        TiltX =    0b0100,
        TiltY =    0b1000
    }

    public enum PointerButtonChangeType : System.UInt32
    {
        //I don't like not defining these, but they did not have values assigned in WinUser.h
        None,
        FirstButtonDown,
        FirstButtonUp,
        SecondButtonDown,
        SecondButtonUp,
        ThirdButtonDown,
        ThirdButtonUp,
        FourthButtonDown,
        FourthButtonUp,
        FifthButtonDown,
        FifthButtonUp
    }

    public struct PointerMessageFlags
    {
        public bool New             { get { return (Flags & PointerFlags.New) > 0; } }
        public bool InRange         { get { return (Flags & PointerFlags.InRange) > 0; } }
        public bool InContact       { get { return (Flags & PointerFlags.InContact) > 0; } }
        public bool FirstButton     { get { return (Flags & PointerFlags.FirstButton) > 0; } }
        public bool SecondButton    { get { return (Flags & PointerFlags.SecondButton) > 0; } }
        public bool ThirdButton     { get { return (Flags & PointerFlags.ThirdButton) > 0; } }
        public bool FourthButton    { get { return (Flags & PointerFlags.FourthButton) > 0; } }
        public bool FifthButton     { get { return (Flags & PointerFlags.FifthButton) > 0; } }
        public bool Primary         { get { return (Flags & PointerFlags.Primary) > 0; } }
        public bool Confidence      { get { return (Flags & PointerFlags.Confidence) > 0; } }
        public bool Cancelled       { get { return (Flags & PointerFlags.Cancelled) > 0; } }
        public bool Down            { get { return (Flags & PointerFlags.Down) > 0; } }
        public bool Update          { get { return (Flags & PointerFlags.Update) > 0; } }
        public bool Up              { get { return (Flags & PointerFlags.Up) > 0; } }
        public bool Wheel           { get { return (Flags & PointerFlags.Wheel) > 0; } }
        public bool HWheel          { get { return (Flags & PointerFlags.HWheel) > 0; } }
        public bool CaptureChanged  { get { return (Flags & PointerFlags.CaptureChanged) > 0; } }

        public readonly PointerFlags Flags;

        public PointerMessageFlags(IntPtr wParam)
        {
            Flags = (PointerFlags)wParam.HiWord(); 
        }

        public PointerMessageFlags(PointerFlags flags)
        {
            Flags = flags;
        }

        public override string ToString()
        {
            return string.Format("{0}", Flags);
        }
    }

    public struct PointerMessageData
    {
        public readonly PointerMessageFlags Flags;

        public int X { get { return (int)msg.LParam.SignedLowWord(); } }
        public int Y { get { return (int)msg.LParam.SignedHiWord(); } }

        public System.UInt32 PointerID { get { return msg.WParam.LowWord(); } }

        public readonly System.Windows.Forms.Message msg;

        public PointerMessageData(System.Windows.Forms.Message message)
        {
            this.msg = message;
            this.Flags = new PointerMessageFlags(message.WParam);
        }

        public override string ToString()
        {
            return string.Format("ID: {0} X: {1} Y: {2}", this.PointerID, this.X, this.Y); 
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TouchPoint : IEquatable<TouchPoint> 
    {
        readonly System.Int32 x; //LONG in the C++ definition
        readonly System.Int32 y; //LONG in the C++ definition

        public bool Equals(TouchPoint other)
        {
            return other.x == this.x && other.y == this.y;
        }

        public bool Equals(object other)
        {
            IEquatable<TouchPoint> IOther = other as IEquatable<TouchPoint>;
            if (IOther == null)
                return false;

            return IOther.Equals(this);
        }

        public override int GetHashCode()
        {  
            return (this.x << 16) + this.y;
        }

        public override string ToString()
        {
            return string.Format("X: {0} Y: {1}", x, y);
        }

        
    }

        [StructLayout(LayoutKind.Sequential)]
    public struct PointerInfo
    {
        public readonly PointerType pointerType;
        public readonly System.UInt32 pointerID;
        public readonly System.UInt32 frameID;
        public readonly PointerFlags pointerFlags;
        public readonly IntPtr sourceDevice;
        public readonly IntPtr hwndTarget;
        public readonly TouchPoint ptPixelLocation;
        public readonly TouchPoint ptHimetricLocation;
        public readonly TouchPoint ptPixelLocationRaw;
        public readonly TouchPoint ptHimetricLocationRaw;
        public readonly System.Int32 dwTime;
        public readonly System.UInt32 historyCount;
        public readonly System.Int32 InputData;
        public readonly System.Int32 dwKeyStates;
        public readonly System.UInt64 PerformanceCount;
        public readonly PointerButtonChangeType ButtonChange;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointerPenInfo
    {
        public readonly PointerInfo pointerInfo;
        public readonly PenFlags flags;
        public readonly PenMask mask;
        public readonly System.UInt32 pressure;
        public readonly System.UInt32 rotation;
        public readonly System.Int32 tiltX;
        public readonly System.Int32 tiltY;

        public bool PenFlagsChange(PointerPenInfo other)
        { 
            PenFlags pen_flags_changed = this.flags ^ other.flags;
            return pen_flags_changed > 0;
        }

        public bool PositioningChange(PointerPenInfo other)
        {
            if (!this.pointerInfo.ptPixelLocationRaw.Equals(other.pointerInfo.ptPixelLocationRaw))
                return true;

            if (PenFlagsChange(other))
                return true; 

            PenMask changed = this.mask ^ other.mask;
            PenMask comparable = this.mask & other.mask;
            if (changed > 0)
                return true;
            
            if (this.pressure != other.pressure && (comparable & PenMask.Pressure) > 0)
                return true;

            if (this.tiltX != other.tiltX && (comparable & PenMask.TiltX) > 0)
                return true;

            if (this.tiltY != other.tiltY && (comparable & PenMask.TiltY) > 0)
                return true;

            if (this.rotation != other.rotation && (comparable & PenMask.Rotation) > 0)
                return true;

            return false;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"{flags} {pointerInfo.ptPixelLocation}");
            sb.Append(pointerInfo.ButtonChange != PointerButtonChangeType.None ? $"{pointerInfo.ButtonChange.ToString()}" : "");
            sb.Append((mask & PenMask.Pressure) > 0 ? $" Pressure: {pressure}" : "");
            sb.Append((mask & PenMask.Rotation) > 0 ? $" Rotation: {rotation}" : "");
            sb.Append((mask & PenMask.TiltX) > 0 ? $" TiltX: {tiltX}" : "");
            sb.Append((mask & PenMask.TiltY) > 0 ? $" TiltY: {tiltY}" : "");
            return sb.ToString();
        }
    }

    public enum TouchMessageType : int
    {
        WM_GESTURE = 0x0119,
        WM_GESTURENOTIFY = 0x011A,

        WM_TOUCHHITTESTING = 0x024D,
        WM_POINTERDEVICEINRANGE = 0X239,
        WM_POINTERDEVICEOUTOFRANGE = 0X23A,
        WM_POINTERUPDATE = 0X245,
        WM_POINTERDOWN = 0X246,
        WM_POINTERUP = 0X247,

        WM_POINTERENTER = 0X249,
        WM_POINTERLEAVE = 0x24A,
        WM_POINTERWHEEL = 0x24E,
        WM_POINTERHWHEEL = 0x024F,
        DM_POINTERHITTEST = 0x0250,

     
}

    /// <summary>
    /// Contains variables that can be adjusted to change user experience
    /// </summary>
    public class TouchConfig
    {
        /// <summary>
        /// Used to convert pen pressure from 0 to 1 range
        /// </summary>
        public double minPenPressure = 0;
        public double maxPenPressure = 1024;

        public double NormalizeStylusPressure(double pressure)
        {
            if (pressure < 0)
            {
                throw new ArgumentException(string.Format("Stylus Pressure not expected to be below minimum: {0} < {1}", pressure, minPenPressure));
            }
            if (pressure > maxPenPressure)
            {
                throw new ArgumentException(string.Format("Stylus Pressure not expected to be above maximum: {0} > {1}", pressure, maxPenPressure));
            }

            double fraction = pressure / maxPenPressure;
            return fraction > 1.0 ? 1.0 : fraction;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WinMsgPoints
    {
        public readonly short x;
        public readonly short y;

        public override string ToString()
        {
            return $"{x}, {y}";
        }

        public static implicit operator System.Drawing.Point(WinMsgPoints obj)
        {
            return new System.Drawing.Point(obj.x, obj.y);
        }
    }

    public static class WinMsgInput
    {
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;

        public const int WM_TOUCHHITTESTING = 0x024D;
        public const int WM_POINTERDEVICEINRANGE = 0X239;
        public const int WM_POINTERDEVICEOUTOFRANGE = 0X23A;
        public const int WM_POINTERUPDATE = 0X245;
        public const int WM_POINTERDOWN = 0X246;
        public const int WM_POINTERUP = 0X247;

        public const int WM_POINTERENTER = 0X249;
        public const int WM_POINTERLEAVE = 0x24A;
        public const int WM_POINTERWHEEL = 0x24E;
        public const int WM_POINTERHWHEEL = 0x024F;
        public const int DM_POINTERHITTEST = 0x0250;

        public const int WM_GESTURE = 0x0119;
        public const int WM_GESTURENOTIFY = 0x011A;

        public static TouchConfig Config = new TouchConfig();

        public static System.UInt16 HiWord(this IntPtr param) { return (System.UInt16)(param.ToInt32() >> 16); } //Shift away the low word
        public static System.UInt16 LowWord(this IntPtr param) { return (System.UInt16)(param.ToInt32() & 0xFFFF); }


        public static System.Int16 SignedHiWord(this IntPtr param)
        {
            UInt16 val = HiWord(param);
            unchecked
            {
                return (Int16)val;
            }
        }

        public static System.Int16 SignedLowWord(this IntPtr param)
        {
            UInt16 val = LowWord(param);
            unchecked
            {
                return (Int16)val;
            }
        }

        public static int HiWord(this int param) { return (int)(param & 0xFFFF0000); }
        public static int LowWord(this int param) { return (int)(param & 0xFFFF); }

        public static uint GetPointerID(IntPtr wParam) { return wParam.LowWord(); }

        public static bool IsPenEvent(out System.UInt32 pointerID)
        {
            uint word = (uint)GetMessageExtraInfo();
            const uint SignatureMask =   0xFFFFFF00;
            const uint PointerIDMask =    0x0000007F;
            const uint MI_WP_SIGNATURE = 0xFF515700;
            uint signature = word & SignatureMask;
            pointerID = word & PointerIDMask;

            return signature == MI_WP_SIGNATURE;
        }

        public static bool IsTouchEvent(out System.UInt32 pointerID)
        {
            uint word = (uint)GetMessageExtraInfo();
            const uint PointerIDMask = 0x0000007F;
            const uint TOUCH_SIGNATURE = 0x80;
            uint signature = word & TOUCH_SIGNATURE;
            pointerID = word & PointerIDMask;

            return signature > 0;
        }


        public static PointerPenInfo GetPenInfo(System.UInt32 pointerID)
        {            //IsPenEvent(out System.UInt32 pointerID);
            //bool gotCursorID = GetPointerCursorId(pointerID, out System.UInt32 cursorID);
            //System.Diagnostics.Debug.Assert(gotCursorID);
            bool gotPenInfo = GetPointerPenInfo(pointerID, out PointerPenInfo info);
            System.Diagnostics.Debug.Assert(gotPenInfo);
            return info;
        }

        public static bool GetPenInfo(out PointerPenInfo info)
        {
            if(IsPenEvent(out uint pointerID))
            {
                info = GetPenInfo(pointerID);
                return true;
            }

            info = new PointerPenInfo();
            return false;
        }


        public static void LogPenData(System.Windows.Forms.Message msg, string Header)
        {
            if (Global.TracePenEvents == false)
                return;

            PointerMessageData data = new PointerMessageData(msg);
            
            bool success = WinMsgInput.GetPointerPenInfo(data.PointerID, out PointerPenInfo info);
            if (success)
            {
                //System.Diagnostics.Trace.WriteLine(string.Format("{0} {1} pen:{2} pointer: {4} pressure:{5} ", Header, data.ToString(), info.flags.ToString(), info.mask.ToString(), info.pointerInfo.pointerFlags, info.pressure));
                System.Diagnostics.Trace.WriteLine(string.Format("{0} {1} pen:{2} pointer: {3} btn: {4} pressure:{5} ",
                    Header,
                    data.ToString(),
                    info.flags.ToString(),
                    info.pointerInfo.pointerFlags,
                    info.pointerInfo.ButtonChange,
                    info.pressure));
            }
        }


        [DllImport("user32.dll")]
        public static extern bool RegisterTouchHitTestingWindow(IntPtr hWnd, TouchHitTesting flags);

        [DllImport("user32.dll")]
        public static extern bool RegisterTouchWindow(IntPtr hWnd, TouchRegisterOptions flags);

        [DllImport("user32.dll")]
        public static extern bool EnableMouseInPointer(bool enabled);

        [DllImport("User32.dll")]
        public static extern bool GetPointerType(System.UInt32 pPointerID, out PointerType pPointerType);

        [DllImport("User32.dll")]
        public static extern bool GetPointerInfo(System.UInt32 pPointerID, out PointerInfo info);

        [DllImport("User32.dll")]
        public static extern bool GetPointerCursorId(System.UInt32 pPointerID, out System.UInt32 cursorID);

        [DllImport("User32.dll")]
        public static extern bool GetPointerPenInfo(System.UInt32 pPointerID, out PointerPenInfo info);

        public static bool SetGestureConfig(IntPtr hWnd, GestureConfig[] configs)
        {
            return SetGestureConfig(hWnd, 0, (UInt32)configs.Length, configs, (UInt32)Marshal.SizeOf<GestureConfig>());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <param name="reserved">must be zero</param>
        /// <param name="cIDs"># of gesture config entries</param>
        /// <param name="configs">gesture config array</param>
        /// <param name="cbSize">size of gesture config structure in bytes</param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        public static extern bool SetGestureConfig(IntPtr hWnd, System.UInt32 reserved, UInt32 cIDs, GestureConfig[] configs, UInt32 cbSize);

        [DllImport("User32.dll")]
        public static extern bool GetGestureInfo(System.IntPtr hGestureInfo, out GestureInfo info);

        [DllImport("User32.dll")]
        public static extern bool CloseGestureInfoHandle(System.IntPtr hGestureInfo);

        [DllImport("User32.dll")]
        public static extern System.IntPtr GetMessageExtraInfo();
    }


}
