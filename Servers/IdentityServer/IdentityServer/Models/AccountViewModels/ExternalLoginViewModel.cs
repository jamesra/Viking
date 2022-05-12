using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.AccountViewModels
{
    public class ExternalLoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
