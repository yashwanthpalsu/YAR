using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reminder.Models.Auth;
using Reminder.Models.DBEntities;
using Reminder.Services;

namespace Reminder.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILoggingService _loggingService;

        public AccountController(IAuthService authService, ILoggingService loggingService)
        {
            _authService = authService;
            _loggingService = loggingService;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Map LoginDto to LoginRequest for the service
            var request = new LoginRequest
            {
                Email = model.Email,
                Password = model.Password,
                RememberMe = model.RememberMe
            };

            var (success, message, user) = await _authService.LoginAsync(request);

            if (success)
            {
                _loggingService.LogInformation("User logged in successfully: {Email}", model.Email);
                
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Map RegisterDto to RegisterRequest for the service
            var request = new RegisterRequest
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Address = model.Address,
                Password = model.Password,
                ConfirmPassword = model.ConfirmPassword,
                AcceptTerms = model.AgreeToTerms
            };

            var (success, message, user) = await _authService.RegisterAsync(request);

            if (success)
            {
                _loggingService.LogInformation("User registered successfully: {Email}", model.Email);
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TermsAndConditions()
        {
            try
            {
                var termsPath = Path.Combine(Directory.GetCurrentDirectory(), "TermsAndConditions.txt");
                if (System.IO.File.Exists(termsPath))
                {
                    var termsContent = System.IO.File.ReadAllText(termsPath);
                    ViewBag.TermsContent = termsContent;
                }
                else
                {
                    ViewBag.TermsContent = "Terms and Conditions not available at this time.";
                }
                
                return View();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error loading Terms and Conditions");
                ViewBag.TermsContent = "Terms and Conditions not available at this time.";
                return View();
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult PrivacyPolicy()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return View("Error");
            }

            var request = new VerifyEmailRequest
            {
                Email = email,
                Token = token
            };

            var (success, message) = await _authService.VerifyEmailAsync(request);

            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Login");
            }

            TempData["ErrorMessage"] = message;
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult VerifyPhone()
        {
            return View(new PhoneVerificationDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPhone(PhoneVerificationDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Map PhoneVerificationDto to VerifyPhoneRequest for the service
            var request = new VerifyPhoneRequest
            {
                PhoneNumber = model.PhoneNumber,
                Code = model.Token
            };

            var (success, message) = await _authService.VerifyPhoneAsync(request);

            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var success = await _authService.ForgotPasswordAsync(model.Email);

            if (success)
            {
                ViewData["SuccessMessage"] = "Password reset email sent. Please check your email.";
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "An error occurred while sending the password reset email.");
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return View("Error");
            }

            var model = new ResetPasswordDto
            {
                Email = email,
                Token = token
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, message) = await _authService.ResetPasswordAsync(model.Email, model.Token, model.Password);

            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        [HttpGet]
        public IActionResult ResendEmailVerification()
        {
            return View(new ResendEmailVerificationDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailVerification(ResendEmailVerificationDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, message) = await _authService.ResendEmailVerificationAsync(model.Email);

            if (success)
            {
                ViewData["SuccessMessage"] = message;
                return View(model);
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        [HttpGet]
        public IActionResult ResendPhoneVerification()
        {
            return View(new ResendPhoneVerificationDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendPhoneVerification(ResendPhoneVerificationDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, message) = await _authService.ResendPhoneVerificationAsync(model.PhoneNumber);

            if (success)
            {
                ViewData["SuccessMessage"] = message;
                return View(model);
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var model = new ProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Address = user.Address,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Profile(ProfileDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            // Update user profile
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;

            var success = await _authService.UpdateProfileAsync(user);

            if (success)
            {
                ViewData["SuccessMessage"] = "Profile updated successfully.";
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Failed to update profile.");
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _authService.GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var success = await _authService.ChangePasswordAsync(user.Id, model.CurrentPassword, model.NewPassword);

            if (success)
            {
                ViewData["SuccessMessage"] = "Password changed successfully.";
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Failed to change password. Please check your current password.");
            return View(model);
        }
    }
} 