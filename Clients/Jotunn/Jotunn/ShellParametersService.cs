using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Specialized;
using System.Diagnostics;
using System.ComponentModel.Composition;

namespace Jotunn
{
    /// <summary>
    /// This readonly class exposes the command line arguments or the web query arguments to modules
    /// It also exposes the XML initialization file as an XDocument
    /// </summary>
    [Export(typeof(IShellParameters))]
    internal class ShellParameterService : IShellParameters
    {
        /// <summary>
        /// This should always have the following properties
        /// Host = The URL of the server hosting the volume, no file name or path
        /// HostPath = The URL of the server and local path hosting the volume.  No filename
        /// </summary>
        internal readonly System.Collections.Specialized.NameValueCollection ArgTable;
        internal XDocument InitializationXML;

        public ShellParameterService(System.Collections.Specialized.NameValueCollection argTable, XDocument InitXML)
        {
            this.ArgTable = argTable;
            this.InitializationXML = InitXML; 
        }

        /// <summary>
        /// Create an instance of the service using the System.Environment and
        /// AppDomain.CurrentDomain.SetupInformation.ActivationArguments
        /// </summary>
        public ShellParameterService()
        {
            this.ArgTable = new System.Collections.Specialized.NameValueCollection();
            List<string> Args = new List<string>(System.Environment.GetCommandLineArgs());
            Args.RemoveAt(0); 
            
            string AppWebsite = "";
            string website = "http://connectomes.utah.edu/Rabbit/Volume.VikingXML";
         //   string homepage = "http://connectomes.utah.edu/";
            
            if (Args.Count > 0)
            {
                website = Args[0];
            }
            else
            {
                bool ShowUsage = true;
                if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null)
                {
                    string[] ClickOnceArgs = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData;
                    if (ClickOnceArgs != null && ClickOnceArgs.Length > 0)
                    {
                        Trace.WriteLine("ActivationArguments: ");
                        foreach (string arg in ClickOnceArgs)
                            Trace.WriteLine(arg, "Viking");

                        string FirstArg = System.Web.HttpUtility.HtmlDecode(ClickOnceArgs[0]);
                        string[] HttpArgs = FirstArg.Split('?');

                        AppWebsite = HttpArgs[0]; //The website we use to launch Viking
                        Trace.WriteLine("Application Website: " + AppWebsite, "Viking");

                        if (HttpArgs.Length == 0)
                        {
                            //Sometimes the only argument passed is the application directory
                            if (!HttpArgs[0].ToLower().EndsWith(".application"))
                            {
                                website = HttpArgs[1];
                                ShowUsage = false;
                            }
                        }
                        //Parse the arguments
                        else if (HttpArgs.Length > 1)
                        {
                            ArgTable = System.Web.HttpUtility.ParseQueryString(HttpArgs[1]);

                            if (ArgTable.HasKeys())
                            {
                                //PORT WPF
                                //UI.State.StartupArguments = QueryTable;
                                string VolumeValue = ArgTable["Volume"];
                                if (VolumeValue != null)
                                {
                                    website = VolumeValue;
                                    ShowUsage = false;
                                }
                            }
                            else
                            {
                                website = HttpArgs[1];
                                ShowUsage = false;
                            }
                        }
                    }
                }

                if (ShowUsage)
                {
                    //Launch the viking home page and exit

                    System.Windows.MessageBox.Show("No volume definition file was specified.  Loading RC1 by default.  You can pass a website as the first argument to launch a different volume, or select a volume definition from the website: http://connectomes.utah.edu/", "Viking", System.Windows.MessageBoxButton.OK);
                    //System.Diagnostics.Process WebBrowser = new System.Diagnostics.Process();
                    //WebBrowser.StartInfo.FileName = homepage;
                    //WebBrowser.Start();
                }
            }

            
            //Make sure the website includes a file, if it does not then include Volume.VikingXML by default
            Uri WebsiteURI = new Uri(website);
            string path = WebsiteURI.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
            if (path.Contains(".") == false)
            {
                if (website.EndsWith("/") == false)
                    website = website + "/";

                website = website + "volume.VikingXML";
                WebsiteURI = new Uri(website); 
            }

            string HostPath = System.IO.Path.GetDirectoryName(website);

            //Add the host property to the properties
            ArgTable.Add("Host", website);
            ArgTable.Add("HostPath", HostPath); 

            //Add the host's subpath to the properties
            //Remove the Host, determine the path of the volume
            
            XDocument XDoc = Utils.IO.Load(WebsiteURI);

            //Update the Volume property in case we trimmed the file name
            this.InitializationXML = XDoc; 
        }

        #region IShellParameters Members

        System.Xml.Linq.XDocument IShellParameters.GetXML
        {
            get {
                return InitializationXML; 
            }
        }

        #endregion

        #region IShellParameters Members

        NameValueCollection IShellParameters.GetArgTable
        {
            get { return ArgTable; }
        }

        #endregion
    }
}
