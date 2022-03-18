namespace Viking.Identity.Server.WebManagement.Models
{
    public class ErrorViewModel
    {
        public string RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public string Details { get; set; }

        public bool ShowDetails => !string.IsNullOrEmpty(Details);
    }
}