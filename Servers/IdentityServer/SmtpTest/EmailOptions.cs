namespace SmtpTest
{
    /// <summary>
    /// Email configuration options for SMTP settings
    /// </summary>
    public class EmailOptions
    {
        public string Server { get; set; }
        public int? Port { get; set; }
        public int? Timeout { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public bool EnableSsl { get; set; }
        public bool UseHtml { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EnableSending { get; set; } = true;
    }
}

