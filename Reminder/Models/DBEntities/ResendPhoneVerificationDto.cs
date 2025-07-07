using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class ResendPhoneVerificationDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
} 