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
        Client = 1, //messages are sent to the target window.
        None = 2 //messages are not sent to the target window or child windows.
    };

    [Flags]
    public enum TouchRegisterOptions
    {
        None = 0,
        FineTouch = 1,
        WantPalm = 2
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
        Barrel = 0x01,
        /// <summary>
        /// The pen is inverted
        /// </summary>
        Inverted = 0x02,
        /// <summary>
        /// The eraser button is pressed
        /// </summary>
        Eraser = 0x04
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
        None = 0x0,
        Pressure = 0x01,
        Rotation = 0x02,
        TiltX = 0x04,
        TiltY = 0x08
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
        public bool New             { get { return (Flags & (int)PointerFlags.New) > 0; } }
        public bool InRange         { get { return (Flags & (int)PointerFlags.InRange) > 0; } }
        public bool InContact       { get { return (Flags & (int)PointerFlags.InContact) > 0; } }
        public bool FirstButton     { get { return (Flags & (int)PointerFlags.FirstButton) > 0; } }
        public bool SecondButton    { get { return (Flags & (int)PointerFlags.SecondButton) > 0; } }
        public bool ThirdButton     { get { return (Flags & (int)PointerFlags.ThirdButton) > 0; } }
        public bool FourthButton    { get { return (Flags & (int)PointerFlags.FourthButton) > 0; } }
        public bool FifthButton     { get { return (Flags & (int)PointerFlags.FifthButton) > 0; } }
        public bool Primary         { get { return (Flags & (int)PointerFlags.Primary) > 0; } }
        public bool Confidence      { get { return (Flags & (int)PointerFlags.Confidence) > 0; } }
        public bool Cancelled       { get { return (Flags & (int)PointerFlags.Cancelled) > 0; } }
        public bool Down            { get { return (Flags & (int)PointerFlags.Down) > 0; } }
        public bool Update          { get { return (Flags & (int)PointerFlags.Update) > 0; } }
        public bool Up              { get { return (Flags & (int)PointerFlags.Up) > 0; } }
        public bool Wheel           { get { return (Flags & (int)PointerFlags.Wheel) > 0; } }
        public bool HWheel          { get { return (Flags & (int)PointerFlags.HWheel) > 0; } }
        public bool CaptureChanged  { get { return (Flags & (int)PointerFlags.CaptureChanged) > 0; } }

        public readonly int Flags;

        public PointerMessageFlags(IntPtr wParam)
        {
            Flags = wParam.LowWord(); 
        }

    }

    public struct PointerMessageData
    {
        public readonly PointerMessageFlags Flags;

        public int X { get { return msg.LParam.LowWord(); } }
        public int Y { get { return msg.LParam.HiWord(); } }

        public System.UInt32 PointerID { get { return msg.WParam.LowWord(); } }

        public readonly System.Windows.Forms.Message msg;

        public PointerMessageData(System.Windows.Forms.Message message)
        {
            this.msg = message;
            this.Flags = new PointerMessageFlags(message.WParam);
        }

        public override string ToString()
        {
            return string.Format("ID: {0} X: {1} Y: {2} Flags: {3}", this.PointerID, this.X, this.Y, this.Flags.Flags); 
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TouchPoint
    {
        readonly System.Int32 x; //LONG in the C++ definition
        readonly System.Int32 y; //LONG in the C++ definition

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
    }

    public static class Touch
    {
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

        public static System.UInt16 HiWord(this IntPtr param) { return (System.UInt16)(param.ToInt32() >> 16); } //Shift away the low word
        public static System.UInt16 LowWord(this IntPtr param) { return (System.UInt16)(param.ToInt32() & 0xFFFF); }

        public static int HiWord(this int param) { return (int)(param & 0xFFFF0000); }
        public static int LowWord(this int param) { return (int)(param & 0xFFFF); }

        public static int GetPointerID(IntPtr wParam) { return wParam.LowWord(); }

        public static bool IsPenEvent(out System.UInt32 cursorID)
        {
            uint word = (uint)GetMessageExtraInfo();
            const uint SignatureMask =   0xFFFFFF00;
            const uint CursorIDMask =    0x0000007F;
            const uint MI_WP_SIGNATURE = 0xFF515700;
            uint signature = word & SignatureMask;
            cursorID = word & CursorIDMask;

            return signature == MI_WP_SIGNATURE;
        }

        public static PointerPenInfo GetPenState()
        {
            IsPenEvent(out System.UInt32 cursorID);
            GetPointerPenInfo(cursorID, out PointerPenInfo info);
            return info;
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
        public static extern bool GetPointerPenInfo(System.UInt32 pPointerID, out PointerPenInfo info);

        [DllImport("User32.dll")]
        public static extern System.IntPtr GetMessageExtraInfo();
    }


}
