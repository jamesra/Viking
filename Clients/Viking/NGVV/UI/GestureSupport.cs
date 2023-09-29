using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Viking.UI
{
    /// <summary>
    /// GC_ZOOM* flags
    /// </summary>
    [Flags]
    public enum GestureZoomConfig
    {
        Zoom = 0x01
    }

    /// <summary>
    /// GC_ZOOM* flags
    /// </summary>
    [Flags]
    public enum GestureRotateConfig
    {
        Rotate = 0x01
    }

    /// <summary>
    /// GC_ZOOM* flags
    /// </summary>
    [Flags]
    public enum GestureTwoFingerTapConfig
    {
        TwoFingerTap = 0x01
    }

    /// <summary>
    /// GC_ZOOM* flags
    /// </summary>
    [Flags]
    public enum GesturePressAndTapConfig
    {
        PressAndTap = 0x01
    }

    /// <summary>
    /// GC_PAN* flags
    /// </summary>
    [Flags]
    public enum GesturePanConfig
    {
        Pan = 0x01,
        PanWithSingleFingerVertically = 0x02,
        PanWithSingleFingerHorizontally = 0x04,
        PanWithGutter = 0x08,
        PanWithInertia = 0x10
    }
      

    [Flags]
    public enum GestureState {
        /// <summary>
        /// A gesture is starting.
        /// </summary>
        GF_BEGIN = 0x01,
        /// <summary>
        /// A gesture has triggered inertia.
        /// </summary>
        GF_INERTIA = 0x02,
        /// <summary>
        /// A gesture has finished.
        /// </summary>
        GF_END = 0x04
    }

    /// <summary>
    /// Gesture IDS, GID_* from WinUser.h
    /// </summary>
    public enum Gesture
    {
        /// <summary>
        /// GID_BEGIN: A gesture is starting.
        /// </summary>
        Begin = 1,
        /// <summary>
        /// GID_END: A gesture is ending
        /// </summary>
        End = 2,
        /// <summary>
        /// GID_ZOOM: The zoom gesture
        /// </summary>
        Zoom = 3,
        /// <summary>
        /// GID_PAN: The pan gesture.
        /// </summary>
        Pan = 4,
        /// <summary>
        /// GID_ROTATE: The rotation gesture.
        /// </summary>
        Rotate = 5,
        /// <summary>
        /// GID_TWOFINGERTAP: The two-finger tap gesture.
        /// </summary>
        TwoFingerTap = 6,
        /// <summary>
        /// GID_PRESSANDTAP: The press and tap gesture.
        /// </summary>
        PressAndTap = 7,
        /// <summary>
        /// GID_ROLLOVER: Same ID as press and tap gesture
        /// </summary>
        Rollover = 7
    }
     
    [StructLayout(LayoutKind.Sequential)]
    public struct GestureConfig
    {
        /// <summary>
        /// Gesture ID
        /// </summary>
        public UInt32 ID;
        /// <summary>
        /// settings related to gesture ID that are to be turned on
        /// </summary>
        public UInt32 Want;
        /// <summary>
        /// settings related to gesture ID that are to be turned off
        /// </summary>
        public UInt32 Block;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct GestureInfo
    {
        /// <summary>
        /// The size of the structure, in bytes. The caller must set this to sizeof(GESTUREINFO).
        /// </summary>
        public UInt32 Size;

        /// <summary>
        /// The state of the gesture. For additional information, see Remarks.
        /// </summary>
        public GestureState State;

        /// <summary>
        /// The identifier of the gesture command.
        /// </summary>
        public UInt32 ID;

        public Gesture Gesture { get { return (Gesture)ID; } }

        /// <summary>
        /// A handle to the window that is targeted by this gesture.
        /// </summary>
        public IntPtr hwndTarget;

        /// <summary>
        /// Current location of this gesture
        /// </summary>
        public WinMsgPoints Location;

        /// <summary>
        /// internally used by Windows
        /// </summary>
        private readonly UInt32 InstanceID;

        /// <summary>
        /// internally used by Windows
        /// </summary>
        private readonly UInt32 SequenceID;

        /// <summary>
        /// Arguments for gestures whose arguments fit in 8 BYTES
        /// </summary>
        public UInt64 Arguments;

        /// <summary>
        /// Size, in bytes, of extra arguments, if any, that accompany this gesture
        /// </summary>
        public UInt32 ExtraArgs;

        public override string ToString()
        {
            return $"{Gesture}: {Location} {State} {InstanceID} {SequenceID} {Arguments}";
        }
    }


    public static class GestureSupport
    {
        public const UInt32 GC_ALLGESTURES  = 1;

        /// <summary>
        /// Enable Pan and Zoom gestures
        /// </summary>
        /// <returns></returns>
        public static bool ConfigureDefaultGestures(IntPtr hwnd)
        {
            GestureConfig[] config = new GestureConfig[2];
            config[0].ID = (UInt32)Gesture.Pan;
            config[0].Want = (UInt32)(GesturePanConfig.PanWithSingleFingerHorizontally |
                             GesturePanConfig.PanWithSingleFingerVertically |
                             GesturePanConfig.PanWithInertia);
            config[0].Block = (UInt32)GesturePanConfig.PanWithGutter;
            config[1].ID = (UInt32)Gesture.Zoom;
            config[1].Want = (UInt32)(GestureZoomConfig.Zoom);
            config[1].Block = 0;

            return WinMsgInput.SetGestureConfig(hwnd, config);
        }


        public static bool ProcessGestureMessage(ref Message msg, out GestureInfo gestureInfo)
        {
            gestureInfo = new GestureInfo();
            if (msg.Msg != WinMsgInput.WM_GESTURE)
                return false;

            gestureInfo.Size = (UInt32)Marshal.SizeOf<GestureInfo>();

            bool gotInfo = false;
            try
            {
                gotInfo = WinMsgInput.GetGestureInfo(msg.LParam, out gestureInfo);
                if (gotInfo == false)
                    return false;

                //Trace.WriteLine($"Gesture: {gestureInfo.Gesture} Location: {gestureInfo.Location}");
            }
            finally
            {
                if(gotInfo)
                {
                    WinMsgInput.CloseGestureInfoHandle(msg.LParam);
                }
            }

            return true;
        }
    }
}
