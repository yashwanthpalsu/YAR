using Reminder.Models.Auth;
using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public interface IAuthService
    {
        Task<(bool success, string message, ApplicationUser? user)> RegisterAsync(RegisterRequest request);
        Task<(bool success, string message, ApplicationUser? user)> LoginAsync(LoginRequest request);
        Task<bool> LogoutAsync();
        Task<(bool success, string message)> VerifyEmailAsync(VerifyEmailRequest request);
        Task<(bool success, string message)> VerifyPhoneAsync(VerifyPhoneRequest request);
        Task<(bool success, string message)> ResendEmailVerificationAsync(string email);
        Task<(bool success, string message)> ResendPhoneVerificationAsync(string phoneNumber);
        Task<ApplicationUser?> GetCurrentUserAsync();
        Task<bool> IsEmailConfirmedAsync(string email);
        Task<bool> IsPhoneConfirmedAsync(string phoneNumber);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> ForgotPasswordAsync(string email);
        Task<(bool success, string message)> ResetPasswordAsync(string email, string token, string newPassword);
        Task<bool> UpdateProfileAsync(ApplicationUser user);
    }
} 