using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.AccountViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
