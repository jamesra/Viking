using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace IdentityServer.Services
{
    public static class EmailSenderExtensions
    {
        public static Task SendEmailConfirmationAsync(this IEmailSender emailSender, string email, string link)
        {
            return emailSender.SendEmailAsync(new string[] { email }, "Confirm your email",
                $"Please confirm your account by clicking this link: <a href='{HtmlEncoder.Default.Encode(link)}'>link</a>");
        }

        public static Task SendUserRegistrationAdminNotificationAsync(this IEmailSender emailSender, string[] emails, string NewUsername, string[] RequestedOrganizations)
        {
            return emailSender.SendEmailAsync(emails, "Viking User registration",
                $"Please confirm your account by clicking this link:");
        }
    }
}
