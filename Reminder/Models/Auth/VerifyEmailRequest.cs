using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.Auth
{
    public class VerifyEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;
    }
} 