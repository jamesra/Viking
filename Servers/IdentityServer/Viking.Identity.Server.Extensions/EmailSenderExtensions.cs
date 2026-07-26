using System.Text.Encodings.Web;
using System.Threading.Tasks; 

namespace Viking.Identity.Server.Services
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

        public static Task SendCollaboratorInviteAsync(
            this IEmailSender emailSender,
            string email,
            string orgName,
            string volumeName,
            string registrationLink)
        {
            var encodedLink = HtmlEncoder.Default.Encode(registrationLink);
            return emailSender.SendEmailAsync(
                new[] { email },
                $"Viking access invitation for {orgName}",
                $"You have been invited to administer the organization <strong>{HtmlEncoder.Default.Encode(orgName)}</strong> " +
                $"and access the volume <strong>{HtmlEncoder.Default.Encode(volumeName)}</strong>.<br/><br/>" +
                $"Create your account using this link: <a href='{encodedLink}'>{encodedLink}</a>");
        }
    }
}
