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

namespace Viking.UI.Forms
{
    
    
    public partial class Logon : Form
    {
        public string authenticationURL;

        public string VolumeURL
        {
            get;
            set;
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
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Viking");}
        }

        protected string KeyFileFullPath
        {
            get { return System.IO.Path.Combine(this.KeyFileFolderPath, this.keyFile); }
        }

        protected string passkey
        {
            get { return "marclab.connectome.utah"; }
        }

        public Logon(string AuthenticationURL, string VolumePath = null)
        {
           VolumeURL = VolumePath;
           this.authenticationURL = AuthenticationURL; 

           keyFile = "usrcrd.vkg";
             
            State.UserCredentials = new NetworkCredential(userName, password);
            
            State.userAccessLevel = "Exit";

            InitializeComponent();

            this.textUsername.Focus();

            this.ActiveControl = textUsername;

            // We can do some checking before this
            this.update_label.Text = "Auth. over secure SSL Connection ";

            if (!System.IO.Directory.Exists(this.KeyFileFolderPath))
            {
                System.IO.Directory.CreateDirectory(this.KeyFileFolderPath);
            }
             
            NetworkCredential cachedCredentials = ReadCredentialsFromEncryptedFile();
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

        void linkLabel1_Click(object sender, System.EventArgs e)
        {
            System.Diagnostics.Process.Start("https://connectomes.utah.edu/Viz/Account/Register");
        }      

        void login_handle(object sender, System.EventArgs e)
        {
            this.update_label.Text = "Authenticating...";

            userName = this.textUsername.Text;

            password = this.textPassword.Text;

            
            if (String.IsNullOrEmpty(userName))
                this.update_label.Text = "Enter Username";

            if (String.IsNullOrEmpty(password))
                this.update_label.Text = "Enter Password";

            this.Credentials = new NetworkCredential(userName, password); 

            string responseData = createConnection();

            if (responseData == "Exit")
            {
                this.update_label.Text = "Oops! Server Error, try again";
                return;
            }


            if (responseData == "Invalid")
            {
                counter++;

                if (counter == 3)
                    this.Close();

                
                this.update_label.Text = "Sorry: Invalid credentials, try again " + counter + "/3" ;
            }
            else
            {
                if (this.textUsername.Text != readUserName)
                    System.IO.File.Delete(this.KeyFileFullPath);

                if (remember_me_check_box.Checked)
                {
                    try
                    {
                        WriteCredentialsInEncryptedFile(new NetworkCredential(userName, password));
                    }
                    catch(IOException except)
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

                this.update_label.Text = "Login Successful! -- Access Level: " + responseData.ToUpper();

                State.userAccessLevel = responseData;

                this.Result = DialogResult.OK;

                this.Close();
            }

        }

        private bool WriteCredentialsInEncryptedFile(NetworkCredential credentials)
        { 
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
            this.update_label.Text = "Authenticating...";

            userName = "anonymous";

            password = "connectome";

            string responseData = createConnection();

            if (responseData == "Read")
            {
                this.update_label.Text = "Anonymous Login Successful! -- Access Level: " + responseData.ToUpper();

                State.userAccessLevel = responseData;

                this.Result = DialogResult.OK;

                this.Close();
            }

            else

                this.update_label.Text = "Oops! Server Error, try again";

        }
       
        string createConnection()
        { 
            string postdata = string.Format("userName={0}&password={1}", userName, password);

            Uri AuthenticationURI = new Uri(authenticationURL + "?" + postdata);

            if (AuthenticationURI.Scheme.ToLower() != "https")
            {
                throw new ArgumentException("Logon UI, createConnection(): Expected to authenticate to an https URI scheme"); 
            }

            
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(authenticationURL+"?" + postdata);
            request.Method = "POST";

            using (StreamWriter stream = new StreamWriter(request.GetRequestStream()))
            {
                stream.Write(postdata);
            }

            // Do not validate server certificate, since its user generated for now
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {

                if (response.StatusCode != HttpStatusCode.OK)
                    this.update_label.Text = response.StatusDescription;
                else
                {
                    using (StreamReader streamRead = new StreamReader(response.GetResponseStream()))
                    {
                        return streamRead.ReadToEnd(); 
                    }
                }
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
            System.Diagnostics.Process.Start("http://connectomes.utah.edu/Viz");
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://connectomes.utah.edu/Viz");
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

            using(MD5CryptoServiceProvider HashProvider  = new MD5CryptoServiceProvider())
            {
                TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

                 // Step 2. Create a new TripleDESCryptoServiceProvider object
                using(TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider())
                {

                    // Step 3. Setup the encoder
                    TDESAlgorithm.Key = TDESKey;
                    TDESAlgorithm.Mode = CipherMode.ECB;
                    TDESAlgorithm.Padding = PaddingMode.PKCS7;

                    // Step 4. Convert the input string to a byte[]
                        DataToEncrypt = UTF8.GetBytes(Message);

                    // Step 5. Attempt to encrypt the string
                    using(ICryptoTransform Encryptor = TDESAlgorithm.CreateEncryptor())
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
            using(MD5CryptoServiceProvider HashProvider  = new MD5CryptoServiceProvider())
            {
                byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

                // Step 2. Create a new TripleDESCryptoServiceProvider object
                using(TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider())
                { 

                    // Step 3. Setup the decoder
                    TDESAlgorithm.Key = TDESKey;
                    TDESAlgorithm.Mode = CipherMode.ECB;
                    TDESAlgorithm.Padding = PaddingMode.PKCS7;

                    // Step 4. Convert the input string to a byte[]
                    DataToDecrypt = Convert.FromBase64String(Message);

                    // Step 5. Attempt to decrypt the string
                    using(ICryptoTransform Decryptor = TDESAlgorithm.CreateDecryptor())
                    {
                        Results = Decryptor.TransformFinalBlock(DataToDecrypt, 0, DataToDecrypt.Length);
                    }

                    TDESAlgorithm.Clear();
                }

                HashProvider.Clear();
            }
             

            // Step 6. Return the decrypted string in UTF8 format
            return UTF8.GetString( Results );
        }

        private void Logon_Load(object sender, EventArgs e)
        {
            if (Settings.Default.VolumeURLs == null)
            {
                Settings.Default.VolumeURLs = new System.Collections.Specialized.StringCollection();
            }
             
            foreach (string url in Settings.Default.VolumeURLs)
            {
                comboVolumeURL.Items.Add(url);
            }

            if (this.VolumeURL != null)
            {
                comboVolumeURL.Text = this.VolumeURL;
                if (!Settings.Default.VolumeURLs.Contains(this.VolumeURL))
                {
                    Settings.Default.VolumeURLs.Insert(0, this.VolumeURL); 
                }
            }
            else if (Settings.Default.VolumeURLs.Count > 0)
                comboVolumeURL.Text = Settings.Default.VolumeURLs[0].ToString();

            if (comboVolumeURL.Text.Length == 0)
            {
                comboVolumeURL.Items.Add("http://internal.connectomes.utah.edu/RC2/");
                comboVolumeURL.Items.Add("http://connectomes.utah.edu/Rabbit/Volume.VikingXML");

                comboVolumeURL.Text = "http://connectomes.utah.edu/Rabbit/Volume.VikingXML";
            }

            

            this.VolumeURL = comboVolumeURL.Text; 
        }


        private void comboVolumeURL_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(comboVolumeURL.SelectedIndex > -1)
                comboVolumeURL.Text = comboVolumeURL.Items[comboVolumeURL.SelectedIndex].ToString(); 
        }

        private string TryAddVikingXMLExtension(string URL)
        {
            string NewURL = URL;

            if (!NewURL.EndsWith(".vikingxml"))
            {
                if (NewURL.EndsWith("/") == false)
                    NewURL = NewURL + '/';

                NewURL += "volume.vikingxml";
            }

            return NewURL;
        }

        private void comboVolumeURL_Validating(object sender, CancelEventArgs e)
        {
            Cursor oldCursor = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            HttpWebRequest request = null;
            WebResponse response = null;

            e.Cancel = false; 

            string NewURL = comboVolumeURL.Text;

            if (!(NewURL.ToLower().StartsWith("http:") ||
               NewURL.ToLower().StartsWith("https:")))
                NewURL = "http://" + NewURL; 

            try
            {
                
                Uri volumeURI = new Uri(NewURL);

                //Make sure the website includes a file, if it does not then include Volume.VikingXML by default
                string path = volumeURI.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
                if (path.Contains(".") == false)
                {
                    NewURL = TryAddVikingXMLExtension(NewURL);
                    volumeURI = new Uri(NewURL); 
                }

                request = WebRequest.Create(volumeURI) as HttpWebRequest;
                if (volumeURI.Scheme.ToLower() == "https")
                    request.Credentials = this.Credentials;
                
                request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);

                response = request.GetResponse();

                if (Settings.Default.VolumeURLs.Contains(NewURL))
                {
                    Settings.Default.VolumeURLs.Remove(NewURL);
                }

                Settings.Default.VolumeURLs.Insert(0, NewURL);
            }
            catch (Exception except)
            {
                e.Cancel = true;

                //Remove a URL that didn't work
                if (Settings.Default.VolumeURLs.Contains(NewURL))
                {
                    DialogResult result = MessageBox.Show(this, "Error loading volume URL, remove from history?\n\n Details:\n " + except.Message, "Invalid Volume URL", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes)
                        Settings.Default.VolumeURLs.Remove(NewURL);
                }
                else
                {
                    DialogResult result = MessageBox.Show(this, "Error loading volume URL:\n\n Details:\n " + except.Message, "Invalid Volume URL", MessageBoxButtons.OK);
                }                
                return;
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null; 
                }

                this.Cursor = oldCursor;
            }

            this.comboVolumeURL.Text = NewURL; 
            this.VolumeURL = NewURL;
        }

        private void Logon_FormClosed(object sender, FormClosedEventArgs e)
        {
            Settings.Default.Save();
        }

        private void textUsername_TextChanged(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void textPassword_TextChanged(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void remember_me_check_box_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {

        }

        private void btnFindOCPVolume_Click(object sender, EventArgs e)
        {             
            FindVolumeForm findVolumeForm = new FindVolumeForm();

            if(findVolumeForm.ShowDialog() == DialogResult.OK)
            {
                string ServerURL = findVolumeForm.ServerURL + "/" + findVolumeForm.VolumeURL;
                this.comboVolumeURL.Text = TryAddVikingXMLExtension(ServerURL);
                this.VolumeURL = ServerURL;
            }
        }
    }
}
