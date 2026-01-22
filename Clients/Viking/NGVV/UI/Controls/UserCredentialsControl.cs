using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Security.AccessControl;
using Viking.UI;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Viking.UI.Controls
{
    public partial class UserCredentialsControl : UserControl
    {
        private static HttpClient CreateHttpClient()
        {
            return new HttpClient(new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Do not validate server certificate for now
            })
            {
                Timeout = TimeSpan.FromSeconds(30) // Add timeout to prevent indefinite hanging
            };
        }

        public string authenticationURL;
        private string userName = UI.State.AnonymousCredentials.UserName;
        private string password = UI.State.AnonymousCredentials.Password;
        public string folderPath;
        public string keyFile;
        private readonly string passkey;
        private readonly string readUserName;
        private readonly int counter = 0;

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

                    FileStream fs = new(folderPath + keyFile, FileMode.Open, FileAccess.Read);


                    StreamReader sr = new(fs);

                    string[] data = DecryptString(sr.ReadToEnd(), passkey).Split(',');

                    this.textUsername.Text = readUserName = data[0];

                    this.textPassword.Text = data[1];

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

        void linkLabel1_Click(object sender, System.EventArgs e) => System.Diagnostics.Process.Start("https://155.100.104.153/Viz/Account/Register");

        async void login_handle(object sender, System.EventArgs e)
        {
            try
            {
                this.update_label.Text = "Authenticating...";

                userName = this.textUsername.Text;

                password = this.textPassword.Text;


                if (userName == "")
                {
                    this.update_label.Text = "Enter Username";
                    return;
                }

                if (password == "")
                {
                    this.update_label.Text = "Enter Password";
                    return;
                }

                this.Credentials = new NetworkCredential(userName, password);

                string responseData = await createConnectionAsync().ConfigureAwait(true);

                if (responseData == "Exit")
                {
                    this.update_label.Text = "Oops! Server Error, try again";
                    return;
                }


                if (responseData == "Invalid")
                {

                    this.update_label.Text = "Sorry: Invalid credentials, try again " + counter + "/3";
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

                            StreamWriter sw = new(fs);

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
            catch (Exception ex)
            {
                this.update_label.Text = "Authentication failed: " + ex.Message;
            }
        }

        private string encryptString(string content, string passkey) => throw new NotImplementedException();

        async void Handle_Anonymmous(object sender, System.EventArgs e)
        {
            try
            {
                this.update_label.Text = "Authenticating...";

                userName = "anonymous";

                password = "connectome";

                string responseData = await createConnectionAsync().ConfigureAwait(true);

                if (responseData == "Read")
                {
                    this.update_label.Text = "Anonymous Login Successful! -- Access Level: " + responseData.ToUpper();

                    State.userAccessLevel = responseData;

                    textUsername.Text = userName;
                    textPassword.Text = "";

                    this.Result = DialogResult.OK;
                }
                else
                {
                    this.update_label.Text = "Oops! Server Error, try again";
                }
            }
            catch (Exception ex)
            {
                this.update_label.Text = "Authentication failed: " + ex.Message;
            }
        }




        async Task<string> createConnectionAsync()
        {
            string postdata = $"userName={userName}&password={password}";

            Uri AuthenticationURI = new(authenticationURL + "?" + postdata);

            if (AuthenticationURI.Scheme.ToLower() != "https")
            {
                throw new ArgumentException("Logon UI, createConnectionAsync(): Expected to authenticate to an https URI scheme");
            }

            try
            {
                using var httpClient = CreateHttpClient();
                using StringContent content = new(postdata, Encoding.UTF8, "application/x-www-form-urlencoded");
                using var response = await httpClient.PostAsync(authenticationURL, content).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    this.update_label.Text = response.ReasonPhrase;
                    return "Exit";
                }

                string responseData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return responseData;
            }
            catch (HttpRequestException ex)
            {
                this.update_label.Text = "Network error: " + ex.Message;
                return "Exit";
            }
            catch (TaskCanceledException ex)
            {
                this.update_label.Text = "Request timed out";
                return "Exit";
            }
            catch (Exception ex)
            {
                this.update_label.Text = "Unexpected error: " + ex.Message;
                return "Exit";
            }
        }

        void username_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e) => btnLogin.Enabled = this.textUsername.Text.Length != 0 && this.textPassword.Text.Length != 0;

        void password_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e) => btnLogin.Enabled = this.textUsername.Text.Length != 0 && this.textPassword.Text.Length != 0;


        void linkLabel3_Click(object sender, System.EventArgs e) => System.Diagnostics.Process.Start("http://prometheus.med.utah.edu/~marclab/");



        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void vikingLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) => System.Diagnostics.Process.Start("http://connectomes.utah.edu");

        private void annotationsLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) => System.Diagnostics.Process.Start("http://155.100.104.153/Viz");

        private void pictureBox1_Click(object sender, EventArgs e) => System.Diagnostics.Process.Start("http://prometheus.med.utah.edu/~marclab/");

        public string EncryptString(string Message, string Passphrase)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new();

            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below

            using MD5 HashProvider = MD5.Create();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

            // Step 2. Create a new TripleDES object
            using TripleDES TDESAlgorithm = TripleDES.Create();

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
                // Objects are automatically disposed with using statements
            }

            // Step 6. Return the encrypted string as a base64 encoded string
            return Convert.ToBase64String(Results);
        }

        public string DecryptString(string Message, string Passphrase)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new();

            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below

            using MD5 HashProvider = MD5.Create();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(Passphrase));

            // Step 2. Create a new TripleDES object
            using TripleDES TDESAlgorithm = TripleDES.Create();

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
                // Objects are automatically disposed with using statements
            }

            // Step 6. Return the decrypted string in UTF8 format
            return UTF8.GetString(Results);
        }

    }
}
