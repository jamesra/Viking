using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Mail;
using System.Threading.Tasks;

namespace IdentityServer.Services
{
    public class SMTPOptions
    {
        public string Server { get; set; }
        public int? Port { get; set; }
        
        public int? Timeout { get; set; }
        public string SourceEmail { get; set; }
        public string SourceDisplayName { get; set; }
        public bool EnableSSL { get; set; }
        public bool UseHTML { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }
    }

    // This class is used by the application to send email for account confirmation and password reset.
    // For more details see https://go.microsoft.com/fwlink/?LinkID=532713
    public class EmailSender : IEmailSender
    {
        //readonly ILogger<EmailSender> Log; 

        public EmailSender(IOptions<SMTPOptions> optionsAccessor, ILogger<EmailSender> logger)
        {
            Options = optionsAccessor.Value;

            if (string.IsNullOrEmpty(Options.SourceEmail))
            {
                logger.LogError("SMTP.SourceEmail configuration parameter not specified.");
            }

            if(string.IsNullOrEmpty(Options.Server))
            {
                logger.LogError("SMTP.Server configuration parameter not specified.");
            }

            //Log = logger;
        }

        public SMTPOptions Options { get; }

        public Task SendEmailAsync(string[] emailAddresses, string subject, string message)
        {
            if (emailAddresses == null || emailAddresses.Length == 0)
                return Task.CompletedTask;

            if(string.IsNullOrEmpty(Options.SourceEmail) || string.IsNullOrEmpty(Options.Server))
            {
                throw new ArgumentException("Required SMTP parameters not configured.  Check the log.");
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

                ss2.EnableSsl = Options.EnableSSL;

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
                    
                    madmin.From = new MailAddress(Options.SourceEmail, Options.SourceDisplayName);

                    madmin.Subject = subject;

                    madmin.Body = message;

                    foreach (string email in emailAddresses)
                    {
                        madmin.To.Add(new MailAddress(email));
                    }

                    madmin.IsBodyHtml = Options.UseHTML;

                    madmin.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

                    ss2.Send(madmin);
                }

                ss2.Dispose(); 
            }

            return Task.CompletedTask;
        }
    }
}
