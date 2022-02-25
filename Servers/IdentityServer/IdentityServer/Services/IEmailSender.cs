using System.Threading.Tasks;

namespace Viking.Identity.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string[] emails, string subject, string message);
    }
}
