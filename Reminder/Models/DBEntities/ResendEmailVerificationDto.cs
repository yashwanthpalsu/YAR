using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class ResendEmailVerificationDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
    }
} 