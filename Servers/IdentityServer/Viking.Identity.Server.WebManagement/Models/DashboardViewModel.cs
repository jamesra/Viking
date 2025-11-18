using System.Collections.Generic;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Models
{
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalOrganizations { get; set; }
        public int TotalVolumes { get; set; }
        public int TotalGroups { get; set; }
        public int TotalSegmentationServices { get; set; }
        
        public List<OrganizationalUnit> UserOrganizations { get; set; } = new List<OrganizationalUnit>();
        public List<Volume> UserVolumes { get; set; } = new List<Volume>();
        public List<SegmentationService> UserSegmentationServices { get; set; } = new List<SegmentationService>();
        
        public string Username { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsAdmin { get; set; }
    }
}

