using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class ItemSelectedViewModel<KEY>
    {
        [Required]
        public KEY Id { get; set; }

        [Required]
        public bool Selected { get; set; }

        public override string ToString()
        {
            return $"{(Selected ? "[X]" : "[ ]")} {Id.ToString()}";
        }
    }

    public class NamedItemSelectedViewModel<KEY> : ItemSelectedViewModel<KEY>
    {  
        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; }
         
        public override string ToString()
        {
            return $"{(Selected ? "[X]" : "[ ]")} {Name}";
        }
    }
}
