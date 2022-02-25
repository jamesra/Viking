using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models.UserViewModels
{
    public class GroupTreeViewModel
    {
        /// <summary>
        /// Unique key
        /// </summary>
        [Required]
        public long Id { get; set; }

        /// <summary>
        /// null if there is no parent
        /// </summary>
        [Required]
        public string Parent { get; set; }

        /// <summary>
        /// Name of the group
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// True if selected
        /// </summary>
        [Required]
        public bool Selected { get; set; }

        /// <summary>
        /// True if children should be shown
        /// </summary>
        [Required]
        public bool Expanded { get; set; }

        [Required]
        public List<GroupTreeViewModel> Children { get; set; }
    }
}
