using System.Threading.Tasks;

namespace IdentityServer.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string[] emails, string subject, string message);
    }
}
