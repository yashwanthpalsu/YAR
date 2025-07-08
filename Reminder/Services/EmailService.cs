using Hangfire;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;

namespace Reminder.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILoggingService _loggingService;

        public EmailService(IConfiguration configuration, ILoggingService loggingService)
        {
            _configuration = configuration;
            _loggingService = loggingService;
        }

        public async Task<bool> SendEmailVerificationAsync(string email, string name, string token)
        {
            try
            {
                var subject = "Verify Your Email - Reminder App";
                var verificationUrl = $"{_configuration["AppUrl"]}/Account/VerifyEmail?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
                
                var body = $@"
                    <h2>Welcome to Reminder App!</h2>
                    <p>Hi {name},</p>
                    <p>Thank you for registering with us. Please verify your email address by clicking the link below:</p>
                    <p><a href='{verificationUrl}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Verify Email</a></p>
                    <p>Or copy and paste this link in your browser: {verificationUrl}</p>
                    <p>This link will expire in 24 hours.</p>
                    <p>If you didn't create an account, please ignore this email.</p>
                    <br>
                    <p>Best regards,<br>Reminder App Team</p>";

                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending email verification to: {Email}", email);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetAsync(string email, string name, string token)
        {
            try
            {
                var subject = "Reset Your Password - Reminder App";
                var resetUrl = $"{_configuration["AppUrl"]}/Account/ResetPassword?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
                
                var body = $@"
                    <h2>Password Reset Request</h2>
                    <p>Hi {name},</p>
                    <p>We received a request to reset your password. Click the link below to create a new password:</p>
                    <p><a href='{resetUrl}' style='background-color: #dc3545; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Reset Password</a></p>
                    <p>Or copy and paste this link in your browser: {resetUrl}</p>
                    <p>This link will expire in 1 hour.</p>
                    <p>If you didn't request a password reset, please ignore this email.</p>
                    <br>
                    <p>Best regards,<br>Reminder App Team</p>";

                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending password reset to: {Email}", email);
                return false;
            }
        }

        public async Task<bool> SendReminderEmailAsync(string email, string name, string reminderMessage, DateTime reminderTime)
        {
            try
            {
                var subject = "Reminder - Reminder App";
                var formattedTime = reminderTime.ToString("MMMM dd, yyyy 'at' h:mm tt");
                
                var body = $@"
                    <h2>Reminder</h2>
                    <p>Hi {name},</p>
                    <p>This is a reminder for:</p>
                    <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #007bff; margin: 15px 0;'>
                        <p style='margin: 0; font-size: 16px;'><strong>{reminderMessage}</strong></p>
                        <p style='margin: 5px 0 0 0; color: #6c757d;'>Scheduled for: {formattedTime}</p>
                    </div>
                    <p>Thank you for using Reminder App!</p>
                    <br>
                    <p>Best regards,<br>Reminder App Team</p>";
                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending reminder email to: {Email}", email);
                return false;
            }
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string name)
        {
            try
            {
                var subject = "Welcome to Reminder App!";
                
                var body = $@"
                    <h2>Welcome to Reminder App!</h2>
                    <p>Hi {name},</p>
                    <p>Thank you for verifying your email address. Your account is now fully activated!</p>
                    <p>You can now:</p>
                    <ul>
                        <li>Create and manage your reminders</li>
                        <li>Set up email, SMS, and call notifications</li>
                        <li>Organize your reminders by importance</li>
                        <li>Track your reminder history</li>
                    </ul>
                    <p>Get started by creating your first reminder!</p>
                    <br>
                    <p>Best regards,<br>Reminder App Team</p>";

                return await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending welcome email to: {Email}", email);
                return false;
            }
        }

        private async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_configuration["Email:From"]));
                email.To.Add(MailboxAddress.Parse(to));
                email.Subject = subject;
                email.Body = new TextPart(TextFormat.Html) { Text = body };

                using var smtp = new SmtpClient();
                
                var port = int.Parse(_configuration["Email:Port"]);
                var secureSocketOptions = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                
                await smtp.ConnectAsync(
                    _configuration["Email:SmtpServer"],
                    port,
                    secureSocketOptions
                );

                await smtp.AuthenticateAsync(
                    _configuration["Email:Username"],
                    _configuration["Email:Password"]
                );

                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                _loggingService.LogInformation("Email sent successfully to: {Email}", to);
                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error sending email to: {Email}. Error: {ErrorMessage}", to, ex.Message);
                return false;
            }
        }

        // Optionally, implement WhatsApp notification via Twilio
        // public async Task<bool> SendWhatsAppReminderAsync(string phoneNumber, string reminderMessage, DateTime reminderTime)
        // {
        //     // Implement WhatsApp sending logic here
        //     return true;
        // }
    }
} 