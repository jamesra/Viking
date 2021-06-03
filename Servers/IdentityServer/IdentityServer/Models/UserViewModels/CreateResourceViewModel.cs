using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.UserViewModels
{
    public class CreateResourceViewModel
    {
        [Display(Name = "Name")]
        [Required]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Resource Type")]
        public virtual string ResourceTypeId { get; set; }

        [Display(Name = "Organizational Unit")]
        public long? ParentId { get; set; }
        public CreateResourceViewModel()
        { }

        public CreateResourceViewModel(CreateResourceViewModel template)
        {
            this.Name = template.Name;
            this.Description = template.Description;
            this.ResourceTypeId = template.ResourceTypeId;
            this.ParentId = template.ParentId;
        }
    }

    public class CreateOrgUnitViewModel : CreateResourceViewModel
    {
        public override string ResourceTypeId => nameof(OrganizationalUnit);
        public CreateOrgUnitViewModel() : base() { }
        public CreateOrgUnitViewModel(CreateResourceViewModel template) : base(template) { }
    }

    public class CreateGroupViewModel : CreateResourceViewModel
    {
        public override string ResourceTypeId => nameof(Group);

        [Display(Name = "Members")]
        public GroupMembershipViewModel Members { get; set; }

        public CreateGroupViewModel() : base() { }

        public CreateGroupViewModel(CreateResourceViewModel template) : base(template) { }
    }

    public class CreateVolumeViewModel : CreateResourceViewModel
    {
        public override string ResourceTypeId => nameof(Volume);

        [Display(Name = "URL of volume")]
        public virtual Uri URL { get; set; }
        public CreateVolumeViewModel() : base() { }
        public CreateVolumeViewModel(CreateResourceViewModel template) : base(template) { }
    }
}
