using System;
using System.Runtime.InteropServices;

namespace Viking.Common
{
    /// <summary>
    /// Verifies Authenticode (PE) signatures using WinVerifyTrust (wintrust.dll).
    /// Used to ensure only signed extension assemblies are loaded in Release builds.
    /// </summary>
    internal static class AuthenticodeVerifier
    {
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        // WINTRUST_ACTION_GENERIC_VERIFY_V2 - Authenticode policy provider for PE files
        private static readonly Guid WinTrustActionGenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionIgnore = 0;

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(
            IntPtr hwnd,
            [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
            ref WinTrustData pWVTData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint cbStruct;
            public IntPtr pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        /// <summary>
        /// Returns true if the file has a valid Authenticode signature (WinVerifyTrust returns 0).
        /// Returns false if unsigned, signature invalid, or verification fails.
        /// </summary>
        public static bool IsAuthenticodeSigned(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string fullPath = System.IO.Path.GetFullPath(filePath);
            if (!System.IO.File.Exists(fullPath))
                return false;

            IntPtr pathPtr = IntPtr.Zero;
            GCHandle fileInfoHandle = default;

            try
            {
                pathPtr = Marshal.StringToCoTaskMemUni(fullPath);

                var fileInfo = new WinTrustFileInfo
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo)),
                    pcwszFilePath = pathPtr,
                    hFile = IntPtr.Zero,
                    pgKnownSubject = IntPtr.Zero
                };

                fileInfoHandle = GCHandle.Alloc(fileInfo, GCHandleType.Pinned);

                var trustData = new WinTrustData
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData)),
                    pPolicyCallbackData = IntPtr.Zero,
                    pSIPClientData = IntPtr.Zero,
                    dwUIChoice = WtdUiNone,
                    fdwRevocationChecks = WtdRevokeNone,
                    dwUnionChoice = WtdChoiceFile,
                    pFile = fileInfoHandle.AddrOfPinnedObject(),
                    dwStateAction = WtdStateActionIgnore,
                    hWVTStateData = IntPtr.Zero,
                    pwszURLReference = IntPtr.Zero,
                    dwProvFlags = 0,
                    dwUIContext = 0,
                    pSignatureSettings = IntPtr.Zero
                };

                int result = WinVerifyTrust(InvalidHandleValue, WinTrustActionGenericVerifyV2, ref trustData);
                return result == 0;
            }
            finally
            {
                if (fileInfoHandle.IsAllocated)
                    fileInfoHandle.Free();
                if (pathPtr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pathPtr);
            }
        }
    }
}
