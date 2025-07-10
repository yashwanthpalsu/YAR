using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class UserViewModel
    {
        public ApplicationUser User { get; set; }
        public bool IsAdmin { get; set; }
    }
} 