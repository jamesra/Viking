using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Viking.Services
{
    /// <summary>
    /// Modern Windows Credential Manager service for secure credential storage.
    /// Uses Windows DPAPI (Data Protection API) for encryption and Windows Credential Manager for storage.
    /// </summary>
    public static class WindowsCredentialManager
    {
        private const string CREDENTIAL_TARGET_NAME = "Viking_UserCredentials";
        private const string CREDENTIAL_COMMENT = "Viking Application User Credentials";

        /// <summary>
        /// Saves user credentials securely using Windows Credential Manager and DPAPI.
        /// </summary>
        /// <param name="username">The username to save</param>
        /// <param name="password">The password to save</param>
        /// <param name="serverUrl">Optional server URL for context</param>
        /// <returns>True if credentials were saved successfully</returns>
        public static bool SaveCredentials(string username, string password, string? serverUrl = null)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return false;

            try
            {
                // Create a credential object with the username and encrypted password
                CredentialData credentialData = new()
                {
                    Username = username,
                    Password = password,
                    ServerUrl = serverUrl ?? string.Empty,
                    SavedAt = DateTime.UtcNow
                };

                // Serialize and encrypt the credential data using DPAPI
                var serializedData = SerializeCredentialData(credentialData);
                var encryptedData = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(serializedData),
                    GetEntropy(),
                    DataProtectionScope.CurrentUser);

                // Save to Windows Credential Manager
                NativeMethods.CREDENTIAL credential = new()
                {
                    Type = NativeMethods.CRED_TYPE_GENERIC,
                    TargetName = Marshal.StringToHGlobalUni(CREDENTIAL_TARGET_NAME),
                    Comment = Marshal.StringToHGlobalUni(CREDENTIAL_COMMENT),
                    CredentialBlobSize = (uint)encryptedData.Length,
                    CredentialBlob = Marshal.AllocHGlobal(encryptedData.Length),
                    Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
                    UserName = Marshal.StringToHGlobalUni(Environment.UserName)
                };

                try
                {
                    Marshal.Copy(encryptedData, 0, credential.CredentialBlob, encryptedData.Length);

                    bool result = NativeMethods.CredWrite(ref credential, 0);
                    return result;
                }
                finally
                {
                    // Free allocated memory
                    if (credential.CredentialBlob != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(credential.CredentialBlob);
                    }
                    if (credential.TargetName != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(credential.TargetName);
                    }
                    if (credential.Comment != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(credential.Comment);
                    }
                    if (credential.UserName != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(credential.UserName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves user credentials from Windows Credential Manager.
        /// </summary>
        /// <returns>NetworkCredential if found, null otherwise</returns>
        public static NetworkCredential? GetCredentials()
        {
            try
            {
                if (!NativeMethods.CredRead(CREDENTIAL_TARGET_NAME, NativeMethods.CRED_TYPE_GENERIC, 0, out IntPtr credentialPtr))
                {
                    return null;
                }

                try
                {
                    var credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credentialPtr);

                    if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                        return null;

                    // Extract and decrypt the credential data
                    var encryptedData = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, encryptedData, 0, (int)credential.CredentialBlobSize);

                    var decryptedData = ProtectedData.Unprotect(encryptedData, GetEntropy(), DataProtectionScope.CurrentUser);
                    var serializedData = Encoding.UTF8.GetString(decryptedData);

                    var credentialData = DeserializeCredentialData(serializedData);
                    return new NetworkCredential(credentialData.Username, credentialData.Password);
                }
                finally
                {
                    NativeMethods.CredFree(credentialPtr);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrieving credentials: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes saved credentials from Windows Credential Manager.
        /// </summary>
        /// <returns>True if credentials were deleted successfully</returns>
        public static bool DeleteCredentials()
        {
            try
            {
                return NativeMethods.CredDelete(CREDENTIAL_TARGET_NAME, NativeMethods.CRED_TYPE_GENERIC, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting credentials: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if credentials exist in Windows Credential Manager.
        /// </summary>
        /// <returns>True if credentials exist</returns>
        public static bool CredentialsExist()
        {
            try
            {
                return NativeMethods.CredRead(CREDENTIAL_TARGET_NAME, NativeMethods.CRED_TYPE_GENERIC, 0, out IntPtr credentialPtr);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets entropy for DPAPI encryption using application-specific data.
        /// </summary>
        private static byte[] GetEntropy()
        {
            // Use application-specific entropy for additional security
            var entropy = Encoding.UTF8.GetBytes("VikingCore_Credential_Storage_v1.0");
            return entropy;
        }

        /// <summary>
        /// Serializes credential data to JSON-like format.
        /// </summary>
        internal static string SerializeCredentialData(CredentialData data) => $"{data.Username}|{data.Password}|{data.ServerUrl ?? ""}|{data.SavedAt:O}";

        /// <summary>
        /// Deserializes credential data written by <see cref="SerializeCredentialData"/>.
        /// The password may itself contain '|', so the password is the text between the first
        /// separator and the third-from-last one; the server URL and timestamp never contain '|'.
        /// </summary>
        /// <exception cref="FormatException">The text does not contain the four '|'-separated fields.</exception>
        internal static CredentialData DeserializeCredentialData(string data)
        {
            int lastSeparator = data.LastIndexOf('|');
            int urlSeparator = lastSeparator > 0 ? data.LastIndexOf('|', lastSeparator - 1) : -1;
            int firstSeparator = data.IndexOf('|');
            if (firstSeparator < 0 || urlSeparator <= firstSeparator)
                throw new FormatException("Stored credential data is not in the expected format.");

            string serverUrl = data.Substring(urlSeparator + 1, lastSeparator - urlSeparator - 1);
            return new CredentialData
            {
                Username = data.Substring(0, firstSeparator),
                Password = data.Substring(firstSeparator + 1, urlSeparator - firstSeparator - 1),
                ServerUrl = string.IsNullOrEmpty(serverUrl) ? null : serverUrl,
                SavedAt = DateTime.Parse(data.Substring(lastSeparator + 1))
            };
        }

        /// <summary>
        /// Internal data structure for credential information.
        /// </summary>
        internal class CredentialData
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string ServerUrl { get; set; }
            public DateTime SavedAt { get; set; }
        }

        /// <summary>
        /// Native Windows API methods for credential management.
        /// </summary>
        private static class NativeMethods
        {
            public const uint CRED_TYPE_GENERIC = 1;
            public const uint CRED_PERSIST_LOCAL_MACHINE = 2;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CREDENTIAL
            {
                public uint Flags;
                public uint Type;
                public IntPtr TargetName;
                public IntPtr Comment;
                public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
                public uint CredentialBlobSize;
                public IntPtr CredentialBlob;
                public uint Persist;
                public uint AttributeCount;
                public IntPtr Attributes;
                public IntPtr TargetAlias;
                public IntPtr UserName;
            }

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CredWrite(ref CREDENTIAL userCredential, uint flags);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credential);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CredDelete(string target, uint type, int reservedFlag);

            [DllImport("advapi32.dll")]
            public static extern void CredFree(IntPtr credential);
        }
    }
}

