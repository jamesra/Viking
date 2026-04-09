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
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IOptions<EmailOptions> optionsAccessor, ILogger<EmailSender> logger)
        {
            Options = optionsAccessor.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(Options.FromEmail))
            {
                logger.LogError("Email.FromEmail configuration parameter not specified.");
            }

            if(string.IsNullOrEmpty(Options.Server))
            {
                logger.LogError("Email.Server configuration parameter not specified.");
            }
        }

        public EmailOptions Options { get; }

        public Task SendEmailAsync(string[] emailAddresses, string subject, string message)
        {
            if (emailAddresses == null || emailAddresses.Length == 0)
            {
                _logger.LogWarning("SendEmailAsync called with no email addresses");
                return Task.CompletedTask;
            }

            if (!Options.EnableSending)
            {
                _logger.LogInformation("Email sending is disabled (EnableSending=false). Skipping send to: {Recipients}", string.Join(", ", emailAddresses));
                return Task.CompletedTask;
            }

            if(string.IsNullOrEmpty(Options.FromEmail) || string.IsNullOrEmpty(Options.Server))
            {
                _logger.LogError("Required Email parameters not configured. FromEmail: {FromEmail}, Server: {Server}", 
                    Options.FromEmail ?? "null", Options.Server ?? "null");
                throw new ArgumentException("Required Email parameters not configured.  Check the log.");
            }

            var smtpPort = Options.Port ?? 25;
            var smtpTimeout = Options.Timeout ?? 30;
            var useSsl = Options.EnableSsl;
            var useCredentials = Options.Username != null && Options.Username.Length > 0;

            _logger.LogInformation("SMTP Send: Connecting to server {Server}:{Port}, SSL: {EnableSsl}, Timeout: {Timeout}s, Credentials: {HasCredentials}, Username: {Username}", 
                Options.Server, smtpPort, useSsl, smtpTimeout, useCredentials, useCredentials ? Options.Username : "N/A");
            _logger.LogInformation("SMTP Send: From: {FromEmail} ({FromName}), To: {Recipients}, Subject: {Subject}", 
                Options.FromEmail, Options.FromName ?? "N/A", string.Join(", ", emailAddresses), subject);
            _logger.LogDebug("SMTP Send: Message body length: {Length} characters, HTML: {IsHtml}", message.Length, Options.UseHtml);

            try
            {
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
                        _logger.LogDebug("SMTP Send: Using authentication with username: {Username}", Options.Username);
                    }
                    else
                    {
                        ss2.UseDefaultCredentials = true;
                        _logger.LogDebug("SMTP Send: Using default credentials");
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

                        _logger.LogInformation("SMTP Send: Attempting to send email via {Server}:{Port}...", Options.Server, ss2.Port);
                        ss2.Send(madmin);
                        _logger.LogInformation("SMTP Send: Email sent successfully. Server: {Server}, Recipients: {Recipients}", 
                            Options.Server, string.Join(", ", emailAddresses));
                    }

                    ss2.Dispose(); 
                }
            }
            catch (System.Net.Mail.SmtpException ex)
            {
                _logger.LogError(ex, "SMTP Send: Failed to send email. Server: {Server}:{Port}, StatusCode: {StatusCode}, Error: {Message}. Recipients: {Recipients}", 
                    Options.Server, smtpPort, ex.StatusCode, ex.Message, string.Join(", ", emailAddresses));
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "SMTP Send: Inner exception details");
                }
                throw;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "SMTP Send: Unexpected error sending email. Server: {Server}:{Port}, Error: {Message}. Recipients: {Recipients}", 
                    Options.Server, smtpPort, ex.Message, string.Join(", ", emailAddresses));
                throw;
            }

            return Task.CompletedTask;
        }
    }
}
