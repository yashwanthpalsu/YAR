using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reminder.Models.DBEntities;
using Reminder.Services;
using System.Security.Claims;

namespace Reminder.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly IAuthService _authService;
        private readonly ILoggingService _loggingService;

        public ChatController(
            IChatService chatService,
            IAuthService authService,
            ILoggingService loggingService)
        {
            _chatService = chatService;
            _authService = authService;
            _loggingService = loggingService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Return empty list since we're not storing chat history in DB
                return View(new List<ChatMessage>());
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error loading chat interface");
                return View("Error");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return Json(new { success = false, message = "Message cannot be empty" });
                }

                var response = await _chatService.ProcessUserInputAsync(currentUser.Id, request.Message);
                
                return Json(new
                {
                    success = response.Success,
                    message = response.Message,
                    intent = response.Intent,
                    requiresFollowUp = response.RequiresFollowUp,
                    followUpQuestion = response.FollowUpQuestion,
                    extractedData = response.ExtractedData
                });
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error processing chat message");
                return Json(new { success = false, message = "An error occurred while processing your message" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearHistory()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Since we're not storing in DB, just return success
                return Json(new { success = true, message = "Chat history cleared" });
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error clearing chat history");
                return Json(new { success = false, message = "An error occurred while clearing chat history" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Return empty history since we're not storing in DB
                return Json(new { success = true, history = new List<ChatMessage>() });
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error retrieving chat history");
                return Json(new { success = false, message = "An error occurred while retrieving chat history" });
            }
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }
} 