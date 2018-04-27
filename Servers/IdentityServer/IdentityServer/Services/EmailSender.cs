using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Mail;
using Microsoft.Extensions.Options;

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
        public EmailSender(IOptions<SMTPOptions> optionsAccessor)
        {
            Options = optionsAccessor.Value;
        }

        public SMTPOptions Options { get; }

        public Task SendEmailAsync(string emailAddress, string subject, string message)
        {
            using (SmtpClient ss2 = new SmtpClient(Options.Server))
            {

                if (Options.Port.HasValue)
                {
                    ss2.Port = Options.Port.Value;
                }

                if (Options.Timeout.HasValue)
                {
                    ss2.Timeout = Options.Timeout.Value;
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

                    madmin.To.Add(new MailAddress(emailAddress));

                    madmin.IsBodyHtml = Options.UseHTML;

                    madmin.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

                    ss2.Send(madmin);
                }
            }

            return Task.CompletedTask;
        }
    }
}
