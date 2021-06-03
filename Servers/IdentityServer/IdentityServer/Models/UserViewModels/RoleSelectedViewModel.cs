using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.UserViewModels
{
    public class RoleSelectedViewModel
    {
        [Required]
        public string Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]

        public bool Selected { get; set; }
    }
}
