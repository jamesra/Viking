using System.ComponentModel.DataAnnotations;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class CollaboratorOnboardingViewModel
    {
        [Required]
        [Display(Name = "VikingXML URL")]
        [Url]
        public string VikingXmlUrl { get; set; }

        public CreateOrgUnitViewModel Org { get; set; } = new CreateOrgUnitViewModel();

        public CreateVolumeViewModel Volume { get; set; } = new CreateVolumeViewModel();

        [Required]
        [EmailAddress]
        [Display(Name = "Collaborator Email")]
        public string CollaboratorEmail { get; set; }
    }

    public class CollaboratorOnboardingCompleteViewModel
    {
        public long OrganizationalUnitId { get; set; }
        public string OrganizationalUnitName { get; set; }
        public long VolumeId { get; set; }
        public string VolumeName { get; set; }
        public string CollaboratorEmail { get; set; }
        public string InviteUrl { get; set; }
        public bool EmailSent { get; set; }
        public bool ExistingUserGranted { get; set; }
        public string EmailError { get; set; }
    }
}
