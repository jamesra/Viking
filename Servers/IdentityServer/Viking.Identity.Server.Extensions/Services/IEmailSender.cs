using System.Threading.Tasks;

namespace Viking.Identity.Server.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string[] emails, string subject, string message);
    }
}
