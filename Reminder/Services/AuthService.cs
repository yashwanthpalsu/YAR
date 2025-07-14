using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.Auth;
using Reminder.Models.DBEntities;
using System.Security.Cryptography;
using System.Text;

namespace Reminder.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly ILoggingService _loggingService;
        private readonly SchedulerDbContext _context;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailService emailService,
            ISmsService smsService,
            ILoggingService loggingService,
            SchedulerDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _smsService = smsService;
            _loggingService = loggingService;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<(bool success, string message, ApplicationUser? user)> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // Validate terms acceptance
                if (!request.AcceptTerms)
                {
                    return (false, "You must accept the Terms and Conditions to register.", null);
                }

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return (false, "User with this email already exists.", null);
                }

                // Check if phone number is already taken
                var existingPhoneUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
                if (existingPhoneUser != null)
                {
                    return (false, "Phone number is already registered.", null);
                }

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Address = request.Address,
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    CreatedAt = DateTime.UtcNow
                };

                // Create user with password
                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _loggingService.LogError("User registration failed: {Errors}", errors);
                    return (false, $"Registration failed: {errors}", null);
                }

                // Generate email verification token
                var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                user.EmailVerificationToken = emailToken;
                user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);

                // Generate phone verification code
                var phoneCode = GenerateVerificationCode();
                user.PhoneVerificationToken = phoneCode;
                user.PhoneVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(10);

                await _userManager.UpdateAsync(user);

                // Send verification emails and SMS
                await _emailService.SendEmailVerificationAsync(user.Email, user.FullName, emailToken);
                await _smsService.SendVerificationCodeAsync(user.PhoneNumber, phoneCode);

                _loggingService.LogInformation("User registered successfully: {Email}", user.Email);
                return (true, "Registration successful. Please check your email and phone for verification.", user);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error during user registration for email: {Email}", request.Email);
                return (false, "An error occurred during registration. Please try again.", null);
            }
        }

        public async Task<(bool success, string message, ApplicationUser? user)> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return (false, "Invalid email or password.", null);
                }

                if (!user.IsActive)
                {
                    return (false, "Account is deactivated. Please contact support.", null);
                }

                var result = await _signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
                
                if (result.Succeeded)
                {
                    // Update last login time
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Ensure Admin role exists
                    if (!await _roleManager.RoleExistsAsync("Admin"))
                    {
                        await _roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Admin"));
                    }

                    // Note: Admin role assignment is now handled by the AdminService
                    // Users can be assigned admin role through the admin interface

                    _loggingService.LogInformation("User logged in successfully: {Email}", user.Email);
                    return (true, "Login successful.", user);
                }
                else if (result.IsLockedOut)
                {
                    return (false, "Account is locked. Please try again later.", null);
                }
                else
                {
                    return (false, "Invalid email or password.", null);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error during login for email: {Email}", request.Email);
                return (false, "An error occurred during login. Please try again.", null);
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                await _signInManager.SignOutAsync();
                _loggingService.LogInformation("User logged out successfully");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error during logout");
                return false;
            }
        }

        public async Task<(bool success, string message)> VerifyEmailAsync(VerifyEmailRequest request)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return (false, "Invalid email address.");
                }

                if (user.EmailConfirmed)
                {
                    return (false, "Email is already verified.");
                }

                if (user.EmailVerificationToken != request.Token)
                {
                    return (false, "Invalid verification token.");
                }

                if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
                {
                    return (false, "Verification token has expired. Please request a new one.");
                }

                user.EmailConfirmed = true;
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenExpiry = null;

                await _userManager.UpdateAsync(user);

                _loggingService.LogInformation("Email verified successfully: {Email}", user.Email);
                return (true, "Email verified successfully.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error during email verification for: {Email}", request.Email);
                return (false, "An error occurred during email verification.");
            }
        }

        public async Task<(bool success, string message)> VerifyPhoneAsync(VerifyPhoneRequest request)
        {
            try
            {
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
                if (user == null)
                {
                    return (false, "Invalid phone number.");
                }

                if (user.PhoneNumberConfirmed)
                {
                    return (false, "Phone number is already verified.");
                }

                if (user.PhoneVerificationToken != request.Code)
                {
                    return (false, "Invalid verification code.");
                }

                if (user.PhoneVerificationTokenExpiry < DateTime.UtcNow)
                {
                    return (false, "Verification code has expired. Please request a new one.");
                }

                user.PhoneNumberConfirmed = true;
                user.PhoneVerificationToken = null;
                user.PhoneVerificationTokenExpiry = null;

                await _userManager.UpdateAsync(user);

                _loggingService.LogInformation("Phone verified successfully: {PhoneNumber}", user.PhoneNumber);
                return (true, "Phone number verified successfully.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error during phone verification for: {PhoneNumber}", request.PhoneNumber);
                return (false, "An error occurred during phone verification.");
            }
        }

        public async Task<(bool success, string message)> ResendEmailVerificationAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return (false, "User not found.");
                }

                if (user.EmailConfirmed)
                {
                    return (false, "Email is already verified.");
                }

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                user.EmailVerificationToken = token;
                user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);

                await _userManager.UpdateAsync(user);
                await _emailService.SendEmailVerificationAsync(user.Email, user.FullName, token);

                _loggingService.LogInformation("Email verification resent: {Email}", user.Email);
                return (true, "Verification email sent successfully.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error resending email verification for: {Email}", email);
                return (false, "An error occurred while sending verification email.");
            }
        }

        public async Task<(bool success, string message)> ResendPhoneVerificationAsync(string phoneNumber)
        {
            try
            {
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
                if (user == null)
                {
                    return (false, "User not found.");
                }

                if (user.PhoneNumberConfirmed)
                {
                    return (false, "Phone number is already verified.");
                }

                var code = GenerateVerificationCode();
                user.PhoneVerificationToken = code;
                user.PhoneVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(10);

                await _userManager.UpdateAsync(user);
                await _smsService.SendVerificationCodeAsync(user.PhoneNumber, code);

                _loggingService.LogInformation("Phone verification resent: {PhoneNumber}", user.PhoneNumber);
                return (true, "Verification code sent successfully.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error resending phone verification for: {PhoneNumber}", phoneNumber);
                return (false, "An error occurred while sending verification code.");
            }
        }

        public async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            try
            {
                return await _userManager.GetUserAsync(_signInManager.Context.User);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error getting current user");
                return null;
            }
        }

        public async Task<bool> IsEmailConfirmedAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                return user?.EmailConfirmed ?? false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error checking email confirmation for: {Email}", email);
                return false;
            }
        }

        public async Task<bool> IsPhoneConfirmedAsync(string phoneNumber)
        {
            try
            {
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
                return user?.PhoneNumberConfirmed ?? false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error checking phone confirmation for: {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (result.Succeeded)
                {
                    _loggingService.LogInformation("Password changed successfully for user: {UserId}", userId);
                }
                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error changing password for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return false;
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _emailService.SendPasswordResetAsync(user.Email, user.FullName, token);

                _loggingService.LogInformation("Password reset email sent: {Email}", user.Email);
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending password reset for: {Email}", email);
                return false;
            }
        }

        public async Task<(bool success, string message)> ResetPasswordAsync(string email, string token, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return (false, "Invalid email address.");
                }

                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (result.Succeeded)
                {
                    _loggingService.LogInformation("Password reset successfully for: {Email}", user.Email);
                    return (true, "Password reset successfully.");
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return (false, $"Password reset failed: {errors}");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error resetting password for: {Email}", email);
                return (false, "An error occurred during password reset.");
            }
        }

        public async Task<bool> UpdateProfileAsync(ApplicationUser user)
        {
            try
            {
                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    _loggingService.LogInformation("Profile updated successfully for user: {UserId}", user.Id);
                }
                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error updating profile for user: {UserId}", user.Id);
                return false;
            }
        }

        private string GenerateVerificationCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var code = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
            return code.ToString("D6");
        }
    }
} 