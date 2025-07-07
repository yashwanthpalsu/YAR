using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class EmailVerificationDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Verification token is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Token must be 6 characters")]
        public string Token { get; set; } = string.Empty;
    }
} 