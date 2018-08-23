using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Security.AccessControl;
using Viking.UI;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Viking.Properties;
using System.Xml.Linq;
using System.Windows.Threading;
using System.Threading.Tasks;
using Utils;

namespace Viking.UI.Forms
{ 
    public partial class LogonASPMembership : Form
    {
        private string _AuthenticationServiceURL = null;
        public string AuthenticationServiceURL
        {
            get { return _AuthenticationServiceURL; }
            set
            {
                _AuthenticationServiceURL = value;
                OnAuthenticationServiceURLChanged(_AuthenticationServiceURL);
            }
        }

        protected string RegistrationURL
        {
            get
            {
                return AuthenticationServiceURL + "/Account/Register";
            }
        }

        protected string AuthenticationURL
        {
            get
            {
                return AuthenticationServiceURL + "/Account/Authenticate";
            }
        }


        private string _VolumeURL;

        public string VolumeURL
        {
            get { return _VolumeURL; }
            set
            {
                _VolumeURL = Viking.Common.Util.AppendDefaultVolumeFilenameIfMissing(value);

                if (_VolumeURL != null)
                {
                    Task.Run(() =>
                    {
                        XDocument document = null;
                        try
                        {
                            document = VolumeModel.Volume.LoadXDocument(_VolumeURL);
                            if (this.IsHandleCreated)
                            {
                                this.BeginInvoke(new System.Action(() => comboVolumeURL.Text = _VolumeURL));
                            }
                        }
                        catch (WebException except)
                        {
                            document = null;
                            SetUpdateText("No volume found at URL");

                            if (Settings.Default.VolumeURLs.Contains(_VolumeURL))
                            {
                                DialogResult result = MessageBox.Show(this, "Error loading volume URL, remove from history?\n\n Details:\n " + except.Message, "Invalid Volume URL", MessageBoxButtons.YesNo);
                                if (result == DialogResult.Yes)
                                    Settings.Default.VolumeURLs.Remove(_VolumeURL);
                            }
                        }

                        if (this.IsHandleCreated)
                        {
                            this.Invoke(new System.Action(() => VolumeDocument = document));
                        }
                    });
                }
                else
                {
                    _VolumeDocument = null;
                }

                OnVolumeURLChanged(_VolumeURL);
            }
        }

        private XDocument _VolumeDocument = null;

        protected XDocument VolumeDocument
        {
            get
            {
                return _VolumeDocument;
            }
            set
            {
                _VolumeDocument = value;
                OnVolumeDocumentChanged(_VolumeDocument);
            }
        }

        private string userName = UI.State.AnonymousCredentials.UserName;
        private string password = UI.State.AnonymousCredentials.Password;
        public string keyFile;
        private string readUserName;
        private int counter = 0;

        public NetworkCredential Credentials = UI.State.AnonymousCredentials;

        public DialogResult Result = DialogResult.Cancel;

        protected string KeyFileFolderPath
        {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Viking"); }
        }

        protected string KeyFileFullPath
        {
            get { return System.IO.Path.Combine(this.KeyFileFolderPath, this.keyFile); }
        }

        protected string passkey
        {
            get { return "marclab.connectome.utah"; }
        }

        public LogonASPMembership(string AuthenticationURL, string VolumePath = null)
        {
            if (VolumePath != null)
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => VolumeURL = VolumePath));

            keyFile = "usrcrd.vkg";

            State.UserCredentials = new NetworkCredential(userName, password);

            State.userAccessLevel = "Exit";

            InitializeComponent();

            DisableLogins();

            this.textUsername.Focus();

            this.ActiveControl = textUsername;

            // We can do some checking before this
            update_label.Text = "Auth. over secure SSL Connection";

            if (!System.IO.Directory.Exists(this.KeyFileFolderPath))
            {
                System.IO.Directory.CreateDirectory(this.KeyFileFolderPath);
            }
#if DEBUG             
            NetworkCredential cachedCredentials = ReadCredentialsFromFile();
#else
            NetworkCredential cachedCredentials = ReadCredentialsFromEncryptedFile();
#endif
            if (cachedCredentials == null)
            {
                this.btnLogin.Enabled = false;
            }
            else
            {
                this.textUsername.Text = readUserName = cachedCredentials.UserName;
                this.textPassword.Text = cachedCredentials.Password;
                this.btnLogin.Enabled = true;
                this.AcceptButton = btnLogin;
            }
        }

        private void SetUpdateText(string text)
        {
            this.BeginInvoke(new System.Action(() => update_label.Text = text));
        }

        private void Logon_Load(object sender, EventArgs e)
        {
            if (Settings.Default.VolumeURLs == null)
            {
                Settings.Default.VolumeURLs = new System.Collections.Specialized.StringCollection();
            }

            for (int i = Settings.Default.VolumeURLs.Count - 1; i >= 0; i--)
            {
                if (Settings.Default.VolumeURLs[0] == null)
                    Settings.Default.VolumeURLs.RemoveAt(i);
            }

            foreach (string url in Settings.Default.VolumeURLs)
            {
                if (url != null)
                    comboVolumeURL.Items.Add(url);
            }

            if (this.VolumeURL != null)
            {
                comboVolumeURL.Text = this.VolumeURL;
                AddToDefaultVolumeURLs(this.VolumeURL);
            }
            else if (Settings.Default.VolumeURLs.Count > 0)
                comboVolumeURL.Text = Settings.Default.VolumeURLs[0].ToString();

            if (comboVolumeURL.Text.Length == 0)
            {
                comboVolumeURL.Items.Add("http://internal.connectomes.utah.edu/RC2/");
                comboVolumeURL.Items.Add("http://connectomes.utah.edu/Rabbit/Volume.VikingXML");

                comboVolumeURL.Text = "http://connectomes.utah.edu/Rabbit/Volume.VikingXML";
            }

            if (VolumeURL == null)
            {
                VolumeURL = comboVolumeURL.Text;
            }

        }


        void linkLabel1_Click(object sender, System.EventArgs e)
        {
            System.Diagnostics.Process.Start("https://connectomes.utah.edu/Viz/Account/Register");
        }

        void login_handle(object sender, System.EventArgs e)
        {
            SetUpdateText("Authenticating...");

            userName = this.textUsername.Text;

            password = this.textPassword.Text;


            if (String.IsNullOrEmpty(userName))
                SetUpdateText("Enter Username");

            if (String.IsNullOrEmpty(password))
                SetUpdateText("Enter Password");

            this.Credentials = new NetworkCredential(userName, password);

            string responseData = createConnection();

            if (responseData == "Exit")
            {
                SetUpdateText("Oops! Server Error, try again");
                return;
            }


            if (responseData == "Invalid")
            {
                counter++;

                if (counter == 3)
                    this.Close();


                SetUpdateText("Sorry: Invalid credentials, try again " + counter + "/3");
            }
            else //Login successful
            {
                if (this.textUsername.Text != readUserName)
                    System.IO.File.Delete(this.KeyFileFullPath);

                if (remember_me_check_box.Checked)
                {
                    try
                    {
#if DEBUG
                        WriteCredentialsInFile(new NetworkCredential(userName, password));
#else
                        WriteCredentialsInEncryptedFile(new NetworkCredential(userName, password));
#endif
                    }
                    catch (IOException except)
                    {
                        MessageBox.Show("An exception occured saving your credentials. Viking will continue but your credentials will not be saved for the next login.\nException message:\n" + except.Message);
                        if (System.IO.File.Exists(this.KeyFileFullPath))
                        {
                            System.IO.File.Delete(this.KeyFileFullPath);
                        }
                    }
                }

                else
                {
                    if (System.IO.File.Exists(this.KeyFileFullPath))
                    {
                        System.IO.File.Delete(this.KeyFileFullPath);
                    }
                }

                SetUpdateText("Login Successful! -- Access Level: " + responseData.ToUpper());

                State.userAccessLevel = responseData;

                this.Result = DialogResult.OK;

                this.Close();
            }

        }


        private bool WriteCredentialsInFile(NetworkCredential credentials)
        {
            if (System.IO.File.Exists(this.KeyFileFullPath))
            {
                System.IO.File.Delete(this.KeyFileFullPath);
            }

            if (!System.IO.File.Exists(this.KeyFileFullPath))
            {
                using (System.IO.FileStream f = System.IO.File.Create(this.KeyFileFullPath))
                {
                    using (StreamWriter sw = new StreamWriter(f))
                    {
                        string content = credentials.UserName + "," + credentials.Password;

                        sw.Write(EncryptString(content, this.passkey));

                        sw.Flush();
                    }
                }

                //File.Encrypt(this.KeyFileFullPath);

                return true;
            }

            return false;
        }

        private bool WriteCredentialsInEncryptedFile(NetworkCredential credentials)
        {
            if (System.IO.File.Exists(this.KeyFileFullPath))
            {
                System.IO.File.Delete(this.KeyFileFullPath);
            }

            if (!System.IO.File.Exists(this.KeyFileFullPath))
            {
                using (System.IO.FileStream f = System.IO.File.Create(this.KeyFileFullPath))
                {
                    using (StreamWriter sw = new StreamWriter(f))
                    {
                        string content = credentials.UserName + "," + credentials.Password;

                        sw.Write(EncryptString(content, this.passkey));

                        sw.Flush();
                    }
                }

                File.Encrypt(this.KeyFileFullPath);

                return true;
            }

            return false;
        }

        private NetworkCredential ReadCredentialsFromFile()
        {
            string keyFileFullPath = this.KeyFileFullPath;
            NetworkCredential credentials = null;
            if (System.IO.File.Exists(KeyFileFullPath))
            {

                try
                {
                    //File.Decrypt(KeyFileFullPath);

                    using (System.IO.FileStream f = new FileStream(KeyFileFullPath, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader sr = new StreamReader(f))
                        {

                            string[] data = DecryptString(sr.ReadToEnd(), passkey).Split(',');

                            credentials = new NetworkCredential(data[0], data[1]);

                            return credentials;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    System.IO.File.Delete(KeyFileFullPath);
                }
            }

            return null;
        }

        private NetworkCredential ReadCredentialsFromEncryptedFile()
        {
            string keyFileFullPath = this.KeyFileFullPath;
            NetworkCredential credentials = null;
            if (System.IO.File.Exists(KeyFileFullPath))
            {

                try
                {
                    File.Decrypt(KeyFileFullPath);

                    using (System.IO.FileStream f = new FileStream(KeyFileFullPath, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader sr = new StreamReader(f))
                        {

                            string[] data = DecryptString(sr.ReadToEnd(), passkey).Split(',');

                            credentials = new NetworkCredential(data[0], data[1]);

                            return credentials;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    System.IO.File.Delete(KeyFileFullPath);
                }
            }

            return null;
        }

        private string encryptString(string content, string passkey)
        {
            throw new NotImplementedException();
        }

        void Handle_Anonymmous(object sender, System.EventArgs e)
        {
            if (this.AuthenticationServiceURL != null)
            {
                SetUpdateText("Authenticating...");

                userName = "anonymous";

                password = "connectome";

                string responseData = createConnection();

                if (responseData == "Read")
                {
                    SetUpdateText("Anonymous Login Successful! -- Access Level: " + responseData.ToUpper());

                    State.userAccessLevel = responseData;

                    this.Result = DialogResult.OK;

                    this.Close();
                }

                else

                    SetUpdateText("Oops! Server Error, try again");
            }
            else
            {
                this.Result = DialogResult.OK;
                State.userAccessLevel = "Read";
                this.Close();
            }
        }

        string createConnection()
        {
            string postdata = string.Format("userName={0}&password={1}", userName, password);

            Uri AuthenticationURI;
            try
            {
                AuthenticationURI = new Uri(this.AuthenticationURL + "?" + postdata);
            }
            catch (UriFormatException e)
            {
                return "Exit";
            }

            if (AuthenticationURI.Scheme.ToLower() != "https")
            {
                throw new ArgumentException("Logon UI, createConnection(): Expected to authenticate to an https URI scheme");
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(AuthenticationURI);
            request.Method = "POST";

            using (StreamWriter stream = new StreamWriter(request.GetRequestStream()))
            {
                stream.Write(postdata);
            }

            // Do not validate server certificate, since its user generated for now
            //ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            try
            {
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    if (response == null)
                        SetUpdateText("Null response");
                    else if (response.StatusCode != HttpStatusCode.OK)
                        SetUpdateText(response.StatusDescription);
                    else
                    {
                        using (StreamReader streamRead = new StreamReader(response.GetResponseStream()))
                        {
                            return streamRead.ReadToEnd();
                        }
                    }
                }
            }
            catch (WebException e)
            {
                SetUpdateText("Failure communicating with authentication server.\n" + e.Message);
                return "Exit";
            }

            return "Exit";
        }

        void username_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (this.textUsername.Text.Length != 0 && this.textPassword.Text.Length != 0)
            {
                btnLogin.Enabled = true;
                this.AcceptButton = btnLogin;

            }
            else
            {
                btnLogin.Enabled = false;
                this.AcceptButton = btnAnonymous;
            }
        }

        void password_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (this.textUsername.Text.Length != 0 && this.textPassword.Text.Length != 0)
            {
                btnLogin.Enabled = true;
                this.AcceptButton = btnLogin;

            }
            else
            {
                btnLogin.Enabled = false;
                this.AcceptButton = btnAnonymous;
            }
        }


        private void marclabLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://marclab.org/");
        }

        private void vikingLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://connectomes.utah.edu");
        }

        private void linkVersionHistory_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://connectomes.utah.edu/client/versionhistory.html");
        }

        private void annotationsLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(this.AuthenticationServiceURL);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(this.RegistrationURL);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://marclab.org/");
        }

        void Logon_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();

        }

        public string EncryptString(string Message, string Passphrase)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();

            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below

            //TripleDESCryptoServiceProvider TDESAlgorithm = null;
            byte[] TDESKey = null;
            byte[] DataToEncrypt = null;
            //ICryptoTransform Encryptor = null; 

            using (MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider())
            {
                TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

                // Step 2. Create a new TripleDESCryptoServiceProvider object
                using (TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider())
                {

                    // Step 3. Setup the encoder
                    TDESAlgorithm.Key = TDESKey;
                    TDESAlgorithm.Mode = CipherMode.ECB;
                    TDESAlgorithm.Padding = PaddingMode.PKCS7;

                    // Step 4. Convert the input string to a byte[]
                    DataToEncrypt = UTF8.GetBytes(Message);

                    // Step 5. Attempt to encrypt the string
                    using (ICryptoTransform Encryptor = TDESAlgorithm.CreateEncryptor())
                    {
                        Results = Encryptor.TransformFinalBlock(DataToEncrypt, 0, DataToEncrypt.Length);
                        TDESAlgorithm.Clear();
                    }
                }
            }

            // Step 6. Return the encrypted string as a base64 encoded string
            return Convert.ToBase64String(Results);
        }

        public string DecryptString(string Message, string Passphrase)
        {
            byte[] Results = new byte[0];
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();

            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below

            byte[] DataToDecrypt = null;
            using (MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider())
            {
                byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

                // Step 2. Create a new TripleDESCryptoServiceProvider object
                using (TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider())
                {

                    // Step 3. Setup the decoder
                    TDESAlgorithm.Key = TDESKey;
                    TDESAlgorithm.Mode = CipherMode.ECB;
                    TDESAlgorithm.Padding = PaddingMode.PKCS7;

                    // Step 4. Convert the input string to a byte[]
                    DataToDecrypt = Convert.FromBase64String(Message);

                    // Step 5. Attempt to decrypt the string
                    using (ICryptoTransform Decryptor = TDESAlgorithm.CreateDecryptor())
                    {
                        Results = Decryptor.TransformFinalBlock(DataToDecrypt, 0, DataToDecrypt.Length);
                    }

                    TDESAlgorithm.Clear();
                }

                HashProvider.Clear();
            }


            // Step 6. Return the decrypted string in UTF8 format
            return UTF8.GetString(Results);
        }

        private void OnVolumeURLChanged(string URL)
        {
            if (this.VolumeURL != null)
            {
                comboVolumeURL.Text = this.VolumeURL;
            }
        }

        private void OnVolumeDocumentChanged(XDocument volume)
        {
            if (volume != null)
            {
                XElement volElem = VolumeModel.Volume.GetVolumeElement(volume);
                if (volElem != null)
                {
                    //We managed to load our URL so add it to our list of defaults
                    AddToDefaultVolumeURLs(this.VolumeURL);
                    this.AuthenticationServiceURL = AuthenticationURLForVolume(volElem);
                }
            }
            else
            {
                this.AuthenticationServiceURL = null;
            }
        }

        private void OnAuthenticationServiceURLChanged(string service)
        {
            if (service == null)
            {
                DisableLogins();
                if (VolumeDocument != null)
                {
                    SetUpdateText("No annotation enabled in volume.");
                }
            }
            else if (service != null && VolumeDocument != null)
            {
                EnableLogins();
                SetUpdateText(null);
            }
        }

        private void DisableLogins()
        {
            groupCredentials.Enabled = false;
        }

        private void EnableLogins()
        {
            groupCredentials.Enabled = true;
            BeginInvoke(new Action(() => createConnection()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="volume"></param>
        /// <returns>Null if no authentication server, otherwise server URL</returns>
        private static string AuthenticationURLForVolume(XElement volume)
        {
            List<XElement> matches = volume.Nodes().Where(n => n.NodeType == System.Xml.XmlNodeType.Element).Cast<XElement>().Where(e => e.Name.LocalName.ToLower() == "volumetoendpoint").ToList();

            if (matches.Count == 0)
                return null;

            XElement elem = matches.First();
            XAttribute attrib = elem.GetAttributeCaseInsensitive("authentication");
            if (attrib != null)
            {
                return attrib.Value;
            }

            return null;
        }


        private void comboVolumeURL_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboVolumeURL.SelectedIndex > -1)
            {
                comboVolumeURL.Text = comboVolumeURL.Items[comboVolumeURL.SelectedIndex].ToString();
                SubmitComboVolumeURLChanges();
            }

        }

        private string TryAddVikingXMLExtension(string URL)
        {
            string NewURL = URL;

            if (!NewURL.ToLower().EndsWith(".vikingxml"))
            {
                if (NewURL.EndsWith("/") == false)
                    NewURL = NewURL + '/';

                NewURL += "volume.vikingxml";
            }

            return NewURL;
        }

        private void AddToDefaultVolumeURLs(string NewURL)
        {
            if (NewURL == null)
                return;

            if (Settings.Default.VolumeURLs.Contains(NewURL))
            {
                Settings.Default.VolumeURLs.Remove(NewURL);
            }

            Settings.Default.VolumeURLs.Insert(0, NewURL);
        }

        private void comboVolumeURL_Validating(object sender, CancelEventArgs e)
        {
            string NewURL = comboVolumeURL.Text;

            if (!(NewURL.ToLower().StartsWith("http:") ||
               NewURL.ToLower().StartsWith("https:")))
                NewURL = "http://" + NewURL;

            try
            {
                Uri volumeURI = new Uri(NewURL);

                this.VolumeURL = NewURL;
                this.comboVolumeURL.Text = NewURL;
            }
            catch (UriFormatException)
            {
                e.Cancel = true;
                SetUpdateText("Invalid valid URL format");
            }
        }

        private void Logon_FormClosed(object sender, FormClosedEventArgs e)
        {
            Settings.Default.Save();
        }

        private void btnFindOCPVolume_Click(object sender, EventArgs e)
        {
            FindVolumeForm findVolumeForm = new FindVolumeForm();

            if (findVolumeForm.ShowDialog() == DialogResult.OK)
            {
                string ServerURL = findVolumeForm.ServerURL + "/" + findVolumeForm.VolumeURL;
                ServerURL = TryAddVikingXMLExtension(ServerURL);
                this.comboVolumeURL.Text = ServerURL;
                this.VolumeURL = ServerURL;
            }
        }

        /// <summary>
        /// When typing in a URL we reset a timer every time a change is made.  If the typing stops the timer ticks and the URL is tested to see if it is a real volume.
        /// The goal is not to submit a server request every keystroke.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboVolumeURL_TextUpdate(object sender, EventArgs e)
        {
            //If the text value is a valid URI, then update.
            string NewURL = comboVolumeURL.Text;

            //Reset our timer
            SubmitURLChangedTimer.Stop();
            SubmitURLChangedTimer.Start();
        }

        private void SubmitComboVolumeURLChanges()
        {
            ///When this timer elapses we submit the URL that has been typed and disable the timer until the URL is changed.
            string NewURL = comboVolumeURL.Text;

            if (!(NewURL.ToLower().StartsWith("http:") ||
               NewURL.ToLower().StartsWith("https:")))
                NewURL = "http://" + NewURL;

            if (NewURL != this.VolumeURL)
            {
                try
                {
                    Uri volumeURI = new Uri(NewURL);

                    this.VolumeURL = NewURL;
                    SubmitURLChangedTimer.Stop();
                }
                catch (UriFormatException)
                {
                    return;
                }
            }
        }

        private void SubmitURLChangedTimer_Tick(object sender, EventArgs e)
        {
            SubmitComboVolumeURLChanges();
        }
    }
}
