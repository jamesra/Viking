using System.Collections.Generic;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class MyRightsViewModel
    {
        public string Username { get; set; }
        public bool IsAuthenticated { get; set; }
        public List<VolumeAccessInfo> AccessibleVolumes { get; set; } = new List<VolumeAccessInfo>();
    }

    public class VolumeAccessInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Endpoint { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
    }
}



