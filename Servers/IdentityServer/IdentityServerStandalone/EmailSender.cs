using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Viking.Identity.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;

namespace Viking.Identity.Server.Standalone
{
    /// <summary>
    /// Email configuration options for SMTP settings
    /// </summary>
    public class EmailOptions
    {
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public string FromEmail { get; set; } = "noreply@yourdomain.com";
        public string FromName { get; set; } = "Viking Identity Server";
        public bool UseSSL { get; set; } = true;
        public bool EnableEmailSending { get; set; } = false; // Set to true when ready to send real emails
    }

    /// <summary>
    /// Implementation of IEmailSender for IdentityServerStandalone using MailKit
    /// </summary>
    public class EmailSender : IEmailSender<ApplicationUser>
    {
        private readonly ILogger<EmailSender> _logger;
        private readonly EmailOptions _emailOptions;

        public EmailSender(ILogger<EmailSender> logger, IOptions<EmailOptions> emailOptions)
        {
            _logger = logger;
            _emailOptions = emailOptions.Value;
        }

        public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        {
            var subject = "Confirm your email address";
            var htmlMessage = $@"
                <h2>Confirm your email address</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href='{confirmationLink}'>Confirm Email Address</a></p>
                <p>If you didn't request this, please ignore this email.</p>";
            
            await SendEmailAsync(email, subject, htmlMessage);
        }

        /// <summary>
        /// Sends an email to the specified user
        /// </summary>
        /// <param name="user">The user to send the email to</param>
        /// <param name="subject">The email subject</param>
        /// <param name="htmlMessage">The HTML email content</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SendEmailAsync(ApplicationUser user, string subject, string htmlMessage)
        {
            await SendEmailAsync(user.Email, subject, htmlMessage);
        }

        /// <summary>
        /// Sends an email to the specified email address using MailKit
        /// </summary>
        /// <param name="email">The email address to send to</param>
        /// <param name="subject">The email subject</param>
        /// <param name="htmlMessage">The HTML email content</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (!_emailOptions.EnableEmailSending)
            {
                _logger.LogInformation("Email sending is disabled. Would send to {Email} with subject '{Subject}'", 
                    email, subject);
                _logger.LogDebug("Email content: {HtmlMessage}", htmlMessage);
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailOptions.FromName, _emailOptions.FromEmail));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = htmlMessage;
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_emailOptions.SmtpServer, _emailOptions.SmtpPort, 
                    _emailOptions.UseSSL ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                
                if (!string.IsNullOrEmpty(_emailOptions.SmtpUsername))
                {
                    await client.AuthenticateAsync(_emailOptions.SmtpUsername, _emailOptions.SmtpPassword);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent successfully to {Email} with subject '{Subject}'", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} with subject '{Subject}'", email, subject);
                throw;
            }
        }

        public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        {
            var subject = "Password Reset Code";
            var htmlMessage = $@"
                <h2>Password Reset Code</h2>
                <p>Your password reset code is: <strong>{resetCode}</strong></p>
                <p>This code will expire in 10 minutes.</p>
                <p>If you didn't request this, please ignore this email.</p>";
            
            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        {
            var subject = "Reset your password";
            var htmlMessage = $@"
                <h2>Reset your password</h2>
                <p>Click the link below to reset your password:</p>
                <p><a href='{resetLink}'>Reset Password</a></p>
                <p>This link will expire in 1 hour.</p>
                <p>If you didn't request this, please ignore this email.</p>";
            
            await SendEmailAsync(email, subject, htmlMessage);
        }
    }
}
