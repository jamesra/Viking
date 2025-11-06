using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SmtpTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("SMTP Test Application");
                Console.WriteLine("====================");
                Console.WriteLine();

                // Get the recipient email address
                string recipientEmail = null;
                if (args.Length > 0)
                {
                    recipientEmail = args[0];
                }
                else
                {
                    Console.Write("Enter recipient email address: ");
                    recipientEmail = Console.ReadLine();
                }

                if (string.IsNullOrWhiteSpace(recipientEmail))
                {
                    Console.WriteLine("Error: Recipient email address is required.");
                    return;
                }

                // Load configuration from management project's appsettings.json
                var configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "Viking.Identity.Server.WebManagement",
                    "appsettings.json"
                );

                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Error: Configuration file not found at: {configPath}");
                    return;
                }

                Console.WriteLine($"Loading configuration from: {configPath}");
                Console.WriteLine();

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(configPath, optional: false, reloadOnChange: false)
                    .Build();

                var emailOptions = configuration.GetSection("Email").Get<EmailOptions>();

                if (emailOptions == null)
                {
                    Console.WriteLine("Error: Email section not found in configuration.");
                    return;
                }

                // Display configuration
                Console.WriteLine("Email Configuration:");
                Console.WriteLine($"  Server: {emailOptions.Server}");
                Console.WriteLine($"  Port: {emailOptions.Port ?? 25}");
                Console.WriteLine($"  From Email: {emailOptions.FromEmail}");
                Console.WriteLine($"  From Name: {emailOptions.FromName ?? "N/A"}");
                Console.WriteLine($"  Enable SSL: {emailOptions.EnableSsl}");
                Console.WriteLine($"  Use HTML: {emailOptions.UseHtml}");
                Console.WriteLine($"  Username: {emailOptions.Username ?? "N/A"}");
                Console.WriteLine($"  Password: {(string.IsNullOrEmpty(emailOptions.Password) ? "N/A" : "***")}");
                Console.WriteLine($"  Timeout: {emailOptions.Timeout ?? 30}s");
                Console.WriteLine($"  Enable Sending: {emailOptions.EnableSending}");
                Console.WriteLine();

                if (!emailOptions.EnableSending)
                {
                    Console.WriteLine("Warning: Email sending is disabled (EnableSending=false).");
                    Console.WriteLine("Exiting without sending email.");
                    return;
                }

                if (string.IsNullOrEmpty(emailOptions.FromEmail) || string.IsNullOrEmpty(emailOptions.Server))
                {
                    Console.WriteLine("Error: Required Email parameters not configured (FromEmail or Server is missing).");
                    return;
                }

                // Send test email
                Console.WriteLine($"Sending test email to: {recipientEmail}");
                Console.WriteLine();

                await SendTestEmailAsync(emailOptions, recipientEmail);

                Console.WriteLine();
                Console.WriteLine("Email sent successfully!");
            }
            catch (SmtpException ex)
            {
                Console.WriteLine();
                Console.WriteLine($"SMTP Error: {ex.Message}");
                Console.WriteLine($"Status Code: {ex.StatusCode}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }

        private static Task SendTestEmailAsync(EmailOptions options, string recipientEmail)
        {
            var smtpPort = options.Port ?? 25;
            var smtpTimeout = options.Timeout ?? 30;
            var useCredentials = !string.IsNullOrEmpty(options.Username);

            Console.WriteLine($"Connecting to SMTP server: {options.Server}:{smtpPort}");
            Console.WriteLine($"SSL: {options.EnableSsl}, Timeout: {smtpTimeout}s, Credentials: {useCredentials}");

            using (var smtpClient = new SmtpClient(options.Server))
            {
                if (options.Port.HasValue)
                {
                    smtpClient.Port = options.Port.Value;
                }

                if (options.Timeout.HasValue)
                {
                    smtpClient.Timeout = options.Timeout.Value * 1000;
                }

                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.EnableSsl = options.EnableSsl;

                if (useCredentials)
                {
                    smtpClient.Credentials = new System.Net.NetworkCredential(options.Username, options.Password);
                }
                else
                {
                    smtpClient.UseDefaultCredentials = true;
                }

                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(options.FromEmail, options.FromName);
                    mailMessage.To.Add(new MailAddress(recipientEmail));
                    mailMessage.Subject = "SMTP Test Email";
                    mailMessage.Body = $@"<html>
<body>
<h2>SMTP Test Email</h2>
<p>This is a test email sent from the SMTP test application.</p>
<p><strong>Configuration Used:</strong></p>
<ul>
<li>Server: {options.Server}</li>
<li>Port: {smtpPort}</li>
<li>From: {options.FromEmail} ({options.FromName ?? "N/A"})</li>
<li>SSL: {options.EnableSsl}</li>
<li>Sent at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</li>
</ul>
<p>If you received this email, the SMTP configuration is working correctly.</p>
</body>
</html>";
                    mailMessage.IsBodyHtml = options.UseHtml;

                    if (!options.UseHtml)
                    {
                        mailMessage.Body = $"SMTP Test Email\n\nThis is a test email sent from the SMTP test application.\n\nConfiguration Used:\n- Server: {options.Server}\n- Port: {smtpPort}\n- From: {options.FromEmail} ({options.FromName ?? "N/A"})\n- SSL: {options.EnableSsl}\n- Sent at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\nIf you received this email, the SMTP configuration is working correctly.";
                    }

                    mailMessage.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

                    Console.WriteLine("Sending email...");
                    smtpClient.Send(mailMessage);
                }
            }

            return Task.CompletedTask;
        }
    }
}

