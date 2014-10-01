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

namespace Viking.UI.Controls
{
    public partial class UserCredentialsControl : UserControl
    {
        public string authenticationURL;
        private string userName = UI.State.AnonymousCredentials.UserName;
        private string password = UI.State.AnonymousCredentials.Password; 
        public string folderPath;
        public string keyFile;
        private string passkey;
        private string readUserName;
        private int counter = 0;

        public NetworkCredential Credentials = UI.State.AnonymousCredentials; 

        public DialogResult Result = DialogResult.Cancel;

        public UserCredentialsControl(string AuthenticationURL)
        {
           this.authenticationURL = AuthenticationURL;
           folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Viking";

           keyFile = "\\usrcrd.vkg";

           passkey = "marclab.connectome.utah";
  
            State.UserCredentials = new NetworkCredential(userName, password);

            State.userAccessLevel = "Exit";

            InitializeComponent();

            this.textUsername.Focus();

            this.ActiveControl = textUsername;

            // We can do some checking before this
            this.update_label.Text = "Auth. over secure SSL Connection ";

            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);

            }

            if (System.IO.File.Exists(folderPath + keyFile))
            {

                try
                {
                    File.Decrypt(folderPath + keyFile);

                     FileStream fs = new FileStream(folderPath + keyFile, FileMode.Open, FileAccess.Read);

                
                    StreamReader sr = new StreamReader(fs);

                    string[] data = DecryptString(sr.ReadToEnd(), passkey).Split(',');

                    this.textUsername.Text = readUserName = String.Copy(data[0]);

                    this.textPassword.Text = String.Copy(data[1]);

                    this.btnLogin.Enabled = true;

                    sr.Close();

                    fs.Close();
                }
                catch (Exception e)
                {
                    System.IO.File.Delete(folderPath + keyFile);
                }

            }

            
        }

        void linkLabel1_Click(object sender, System.EventArgs e)
        {
            System.Diagnostics.Process.Start("https://155.100.104.153/Viz/Account/Register");
        }      

        void login_handle(object sender, System.EventArgs e)
        {
            this.update_label.Text = "Authenticating...";

            userName = this.textUsername.Text;

            password = this.textPassword.Text;

            
            if (userName == "")
                this.update_label.Text = "Enter Username";

            if (password == "")
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
                
                this.update_label.Text = "Sorry: Invalid credentials, try again " + counter + "/3" ;
            }
            else
            {
                if (this.textUsername.Text != readUserName)
                    System.IO.File.Delete(folderPath + keyFile);

                if (remember_me_check_box.Checked)
                {
                    if (!System.IO.File.Exists(folderPath + keyFile))
                    {
                        FileStream fs = System.IO.File.Create(folderPath + keyFile);

                        StreamWriter sw = new StreamWriter(fs);

                        string content = userName + "," + password;

                        string encrypted = EncryptString(content, passkey);

                        sw.Write(encrypted);

                        sw.Flush();

                        sw.Close();
                       
                        fs.Close();

                        File.Encrypt(folderPath + keyFile);


                    }
                }

                else
                {
                    if (System.IO.File.Exists(folderPath + keyFile))
                    {
                        System.IO.File.Delete(folderPath + keyFile);
                    }
                }

                this.update_label.Text = "Login Successful! -- Access Level: " + responseData.ToUpper();

                State.userAccessLevel = responseData;

                this.Result = DialogResult.OK;
            }

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

                textUsername.Text = userName;
                textPassword.Text = ""; 

                this.Result = DialogResult.OK;
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

            StreamWriter stream = new StreamWriter(request.GetRequestStream());

            stream.Write(postdata);

            stream.Close();

            // Do not validate server certificate, since its user generated for now
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK)
                this.update_label.Text = response.StatusDescription;
            else
            {
                StreamReader streamRead = new StreamReader(response.GetResponseStream());

                string responseData = streamRead.ReadToEnd();

                return responseData;
            }

            return "Exit";
        }

        void username_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (this.textUsername.Text.Length != 0 && this.textPassword.Text.Length != 0)
            {
                btnLogin.Enabled = true;

            }
            else
            {
                btnLogin.Enabled = false;
            }
        }

        void password_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (this.textUsername.Text.Length != 0 && this.textPassword.Text.Length != 0)
            {
                btnLogin.Enabled = true;

            }
            else
            {
                btnLogin.Enabled = false;
            }
        }


        void linkLabel3_Click(object sender, System.EventArgs e)
        {
            System.Diagnostics.Process.Start("http://prometheus.med.utah.edu/~marclab/");
        }

     

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void vikingLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://connectomes.utah.edu");
        }

        private void annotationsLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://155.100.104.153/Viz");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://prometheus.med.utah.edu/~marclab/");
        }
        
        public string EncryptString(string Message, string Passphrase)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();

            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below

            MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

            // Step 2. Create a new TripleDESCryptoServiceProvider object
            TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider();

            // Step 3. Setup the encoder
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;

            // Step 4. Convert the input string to a byte[]
            byte[] DataToEncrypt = UTF8.GetBytes(Message);

            // Step 5. Attempt to encrypt the string
            try
            {
                ICryptoTransform Encryptor = TDESAlgorithm.CreateEncryptor();
                Results = Encryptor.TransformFinalBlock(DataToEncrypt, 0, DataToEncrypt.Length);
            }
            finally
            {
                // Clear the TripleDes and Hashprovider services of any sensitive information
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }

            // Step 6. Return the encrypted string as a base64 encoded string
            return Convert.ToBase64String(Results);
        }

        public string DecryptString(string Message, string Passphrase)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();

            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below

            MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

            // Step 2. Create a new TripleDESCryptoServiceProvider object
            TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider();

            // Step 3. Setup the decoder
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;

            // Step 4. Convert the input string to a byte[]
            byte[] DataToDecrypt = Convert.FromBase64String(Message);

            // Step 5. Attempt to decrypt the string
            try
            {
                ICryptoTransform Decryptor = TDESAlgorithm.CreateDecryptor();
                Results = Decryptor.TransformFinalBlock(DataToDecrypt, 0, DataToDecrypt.Length);
            }
            finally
            {
                // Clear the TripleDes and Hashprovider services of any sensitive information
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }

            // Step 6. Return the decrypted string in UTF8 format
                return UTF8.GetString( Results );
        }

    }
}
