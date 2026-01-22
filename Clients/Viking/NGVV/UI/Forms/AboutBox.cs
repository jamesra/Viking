using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace Viking.UI.Forms
{
    partial class AboutBox : Form
    {
        public AboutBox()
        {
            InitializeComponent();
            this.Text = String.Format("About {0}", AssemblyTitle);
            this.labelProductName.Text = AssemblyProduct;
            this.labelVersion.Text = String.Format("Version {0}", AssemblyVersion);
            this.labelCopyright.Text = AssemblyCopyright;
            this.labelCompanyName.Text = AssemblyCompany;
            this.textBoxDescription.Text = AssemblyDescription;
            DateTime buildTime = AssemblyBuildDate;
            this.labelBuildDate.Text = "Build Date: " + buildTime.ToShortDateString() + " at " + buildTime.ToLongTimeString();

        }

        #region Assembly Attribute Accessors

        public static string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (!String.IsNullOrEmpty(titleAttribute.Title))
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            }
        }

        public static string AssemblyVersion => Assembly.GetEntryAssembly().GetName().Version.ToString();

        public static string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public static string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public static string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public static string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }

        public static DateTime AssemblyBuildDate
        {
            get
            {
                try
                {
                    // Get the build date from the executable file's last write time
                    // This reflects when the assembly was actually built
                    var assembly = Assembly.GetEntryAssembly();
                    if (assembly != null && !string.IsNullOrEmpty(assembly.Location))
                    {
                        var fileInfo = new System.IO.FileInfo(assembly.Location);
                        if (fileInfo.Exists)
                        {
                            return fileInfo.LastWriteTime;
                        }
                    }
                    
                    // Fallback: Try to use linker timestamp if available
                    // The linker timestamp is embedded in the PE header
                    var assemblyLocation = assembly?.Location;
                    if (!string.IsNullOrEmpty(assemblyLocation) && System.IO.File.Exists(assemblyLocation))
                    {
                        // Read PE header timestamp
                        using (var fs = new System.IO.FileStream(assemblyLocation, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var reader = new System.IO.BinaryReader(fs);
                            fs.Seek(0x3C, System.IO.SeekOrigin.Begin); // Offset to PE signature
                            int peOffset = reader.ReadInt32();
                            fs.Seek(peOffset + 8, System.IO.SeekOrigin.Begin); // Skip PE signature, read timestamp
                            uint timestamp = reader.ReadUInt32();
                            
                            // Convert Unix timestamp to DateTime
                            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            return epoch.AddSeconds(timestamp).ToLocalTime();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting build date: {ex.Message}");
                }

                // Fallback to file timestamp if PE timestamp fails
                try
                {
                    var assembly = Assembly.GetEntryAssembly();
                    if (assembly != null && !string.IsNullOrEmpty(assembly.Location))
                    {
                        var fileInfo = new System.IO.FileInfo(assembly.Location);
                        if (fileInfo.Exists)
                        {
                            return fileInfo.LastWriteTime;
                        }
                    }
                }
                catch
                {
                    // If all else fails, return current date as fallback
                }

                return DateTime.Now;
            }
        }
        #endregion


    }
}
