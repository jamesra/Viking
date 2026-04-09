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
                string? recipientEmail = null;
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
                var recipient = recipientEmail.Trim();

                // Load configuration from management project's appsettings.json
                var configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
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

                // Get the directory containing the config file (WebManagement directory)
                var configDir = Path.GetDirectoryName(configPath)!;
                
                // Load secrets.json from the same directory (will be linked from WebManagement)
                var secretsPath = Path.Combine(configDir, "secrets.json");
                
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(configPath, optional: false, reloadOnChange: false)
                    .AddJsonFile(secretsPath, optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();

                var emailOptions = configuration.GetSection("Email").Get<EmailOptions>();

                if (emailOptions == null)
                {
                    emailOptions = new EmailOptions();
                    // Fallback: read individual keys if section binding didn't work
                    emailOptions.Server = configuration["Email:Server"];
                    emailOptions.Port = int.TryParse(configuration["Email:Port"], out var p) ? p : null;
                    emailOptions.FromEmail = configuration["Email:FromEmail"];
                    emailOptions.FromName = configuration["Email:FromName"];
                    emailOptions.Username = configuration["Email:Username"];
                    emailOptions.Password = configuration["Email:Password"];
                    emailOptions.Timeout = int.TryParse(configuration["Email:Timeout"], out var t) ? t : null;
                    if (bool.TryParse(configuration["Email:EnableSsl"], out var enableSsl))
                    {
                        emailOptions.EnableSsl = enableSsl;
                    }
                    if (bool.TryParse(configuration["Email:UseHtml"], out var useHtml))
                    {
                        emailOptions.UseHtml = useHtml;
                    }
                    if (bool.TryParse(configuration["Email:EnableSending"], out var enableSending))
                    {
                        emailOptions.EnableSending = enableSending;
                    }
                    else
                    {
                        emailOptions.EnableSending = true;
                    }
                }

                // Overlay password from secrets/environment if not already populated
                var configuredPassword = configuration["Email:Password"];
                if (!string.IsNullOrWhiteSpace(configuredPassword))
                {
                    emailOptions.Password = configuredPassword;
                }

                // Display configuration
                Console.WriteLine("Email Configuration:");
                Console.WriteLine($"  Server: {emailOptions.Server ?? "N/A"}");
                Console.WriteLine($"  Port: {emailOptions.Port ?? 25}");
                Console.WriteLine($"  From Email: {emailOptions.FromEmail ?? "N/A"}");
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
                Console.WriteLine($"Sending test email to: {recipient}");
                Console.WriteLine();

                await SendTestEmailAsync(emailOptions, recipient);

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
            var server = options.Server ?? throw new ArgumentException("Server cannot be null", nameof(options));
            var fromEmail = options.FromEmail ?? throw new ArgumentException("FromEmail cannot be null", nameof(options));
            var fromName = options.FromName ?? string.Empty;

            var smtpPort = options.Port ?? 25;
            var smtpTimeout = options.Timeout ?? 30;
                var useCredentials = !string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password);

            Console.WriteLine($"Connecting to SMTP server: {server}:{smtpPort}");
            Console.WriteLine($"SSL: {options.EnableSsl}, Timeout: {smtpTimeout}s, Credentials: {useCredentials}");

            using (var smtpClient = new SmtpClient(server))
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
                    smtpClient.UseDefaultCredentials = false;
                }
                else
                {
                    smtpClient.UseDefaultCredentials = true;
                }

                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(fromEmail, fromName);
                    mailMessage.To.Add(new MailAddress(recipientEmail));
                    mailMessage.Subject = "SMTP Test Email";
                    mailMessage.Body = $@"<html>
<body>
<h2>SMTP Test Email</h2>
<p>This is a test email sent from the SMTP test application.</p>
<p><strong>Configuration Used:</strong></p>
<ul>
<li>Server: {server}</li>
<li>Port: {smtpPort}</li>
<li>From: {fromEmail} ({options.FromName ?? "N/A"})</li>
<li>SSL: {options.EnableSsl}</li>
<li>Sent at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</li>
</ul>
<p>If you received this email, the SMTP configuration is working correctly.</p>
</body>
</html>";
                    mailMessage.IsBodyHtml = options.UseHtml;

                    if (!options.UseHtml)
                    {
                        mailMessage.Body = $"SMTP Test Email\n\nThis is a test email sent from the SMTP test application.\n\nConfiguration Used:\n- Server: {server}\n- Port: {smtpPort}\n- From: {fromEmail} ({options.FromName ?? "N/A"})\n- SSL: {options.EnableSsl}\n- Sent at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\nIf you received this email, the SMTP configuration is working correctly.";
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

