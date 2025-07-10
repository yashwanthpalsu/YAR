using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Reminder.Models.DBEntities
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
        // Admin status is now managed through ASP.NET Core Identity roles
        // Use UserManager.IsInRoleAsync(user, "Admin") to check admin status

        // Custom verification tokens (separate from Identity's built-in ones)
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        public string? PhoneVerificationToken { get; set; }
        public DateTime? PhoneVerificationTokenExpiry { get; set; }

        // Navigation property for reminders
        public virtual ICollection<ReminderViewModel> Reminders { get; set; } = new List<ReminderViewModel>();

        // Computed property for full name
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
} 