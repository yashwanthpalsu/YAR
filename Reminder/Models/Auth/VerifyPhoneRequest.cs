using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.Auth
{
    public class VerifyPhoneRequest
    {
        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;
    }
} 