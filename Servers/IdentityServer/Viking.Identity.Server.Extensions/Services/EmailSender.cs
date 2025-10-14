using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Viking.Identity.Server.Services
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
        public bool EnableSending { get; set; } = true; // Default to true for backward compatibility
    }

    // This class is used by the application to send email for account confirmation and password reset.
    // For more details see https://go.microsoft.com/fwlink/?LinkID=532713
    public class EmailSender : IEmailSender
    {
        //readonly ILogger<EmailSender> Log; 

        public EmailSender(IOptions<EmailOptions> optionsAccessor, ILogger<EmailSender> logger)
        {
            Options = optionsAccessor.Value;

            if (string.IsNullOrEmpty(Options.FromEmail))
            {
                logger.LogError("Email.FromEmail configuration parameter not specified.");
            }

            if(string.IsNullOrEmpty(Options.Server))
            {
                logger.LogError("Email.Server configuration parameter not specified.");
            }

            //Log = logger;
        }

        public EmailOptions Options { get; }

        public Task SendEmailAsync(string[] emailAddresses, string subject, string message)
        {
            if (emailAddresses == null || emailAddresses.Length == 0)
                return Task.CompletedTask;

            if (!Options.EnableSending)
            {
                // Email sending is disabled, just log and return
                return Task.CompletedTask;
            }

            if(string.IsNullOrEmpty(Options.FromEmail) || string.IsNullOrEmpty(Options.Server))
            {
                throw new ArgumentException("Required Email parameters not configured.  Check the log.");
            }

            using (SmtpClient ss2 = new SmtpClient(Options.Server))
            {

                if (Options.Port.HasValue)
                {
                    ss2.Port = Options.Port.Value;
                }

                if (Options.Timeout.HasValue)
                {
                    ss2.Timeout = Options.Timeout.Value * 1000;
                }

                ss2.DeliveryMethod = SmtpDeliveryMethod.Network;

                ss2.EnableSsl = Options.EnableSsl;

                if (Options.Username != null && Options.Username.Length > 0)
                {
                    ss2.Credentials = new System.Net.NetworkCredential(Options.Username, Options.Password);
                }
                else
                {
                    ss2.UseDefaultCredentials = true;
                }


                using (MailMessage madmin = new MailMessage())
                {
                    
                    madmin.From = new MailAddress(Options.FromEmail, Options.FromName);

                    madmin.Subject = subject;

                    madmin.Body = message;

                    foreach (string email in emailAddresses)
                    {
                        madmin.To.Add(new MailAddress(email));
                    }

                    madmin.IsBodyHtml = Options.UseHtml;

                    madmin.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

                    ss2.Send(madmin);
                }

                ss2.Dispose(); 
            }

            return Task.CompletedTask;
        }
    }
}
