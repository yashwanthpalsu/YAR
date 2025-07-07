using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class PhoneVerificationDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Verification token is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Token must be 6 characters")]
        public string Token { get; set; } = string.Empty;
    }
} 