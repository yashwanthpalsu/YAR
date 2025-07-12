using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reminder.Models;
using Reminder.Models.DBEntities;
using Reminder.Services;
using System.Security.Claims;

namespace Reminder.Controllers.API
{
    [Authorize]
    [ApiController]
    [Route("api/chat")]
    public class ChatApiController : ControllerBase
    {
        private readonly IReminderService _reminderService;
        private readonly IAuthService _authService;
        private readonly ILoggingService _loggingService;

        public ChatApiController(
            IReminderService reminderService,
            IAuthService authService,
            ILoggingService loggingService)
        {
            _reminderService = reminderService;
            _authService = authService;
            _loggingService = loggingService;
        }

        [HttpGet("reminders")]
        public async Task<IActionResult> GetUserReminders()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var reminders = await _reminderService.GetUserRemindersAsync(currentUser.Id);
                var reminderList = reminders.Select(r => new
                {
                    id = r.ReminderId,
                    title = r.Name,
                    description = r.Message,
                    schedules = r.Schedules.Select(s => new
                    {
                        id = s.ScheduleId,
                        reminderTime = s.Date.Add(s.Time),
                        isReminderSent = s.IsReminderSent,
                        reminderMode = "Email" // Default mode
                    })
                });

                return Ok(new { success = true, reminders = reminderList });
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error getting user reminders for chat API");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost("reminders")]
        public async Task<IActionResult> CreateReminder([FromBody] CreateReminderApiRequest request)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(new { success = false, message = "Title is required" });
                }

                var reminderRequest = new ReminderViewModel
                {
                    Name = request.Title,
                    Message = request.Description ?? "",
                    UserId = currentUser.Id
                };

                var result = await _reminderService.CreateReminderAsync(reminderRequest);
                
                if (result)
                {
                    // Create schedule if datetime is provided
                    if (request.ReminderTime.HasValue)
                    {
                        var scheduleRequest = new ScheduleViewModel
                        {
                            ReminderId = 0, // Will be set by the service
                            Date = request.ReminderTime.Value.Date,
                            Time = request.ReminderTime.Value.TimeOfDay
                        };

                        // Note: Schedule creation would need to be implemented in the service
                    }

                    return Ok(new { 
                        success = true, 
                        message = $"Reminder '{request.Title}' created successfully"
                    });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to create reminder" });
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error creating reminder via chat API");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPut("reminders/{id}")]
        public async Task<IActionResult> UpdateReminder(int id, [FromBody] UpdateReminderApiRequest request)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var reminder = await _reminderService.GetReminderByIdAsync(id);
                if (reminder == null || reminder.UserId != currentUser.Id)
                {
                    return NotFound(new { success = false, message = "Reminder not found" });
                }

                // Update reminder
                if (!string.IsNullOrWhiteSpace(request.Title))
                    reminder.Name = request.Title;
                if (!string.IsNullOrWhiteSpace(request.Description))
                    reminder.Message = request.Description;

                var result = await _reminderService.UpdateReminderAsync(reminder);
                
                if (result)
                {
                    return Ok(new { success = true, message = "Reminder updated successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to update reminder" });
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error updating reminder via chat API");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpDelete("reminders/{id}")]
        public async Task<IActionResult> DeleteReminder(int id)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var reminder = await _reminderService.GetReminderByIdAsync(id);
                if (reminder == null || reminder.UserId != currentUser.Id)
                {
                    return NotFound(new { success = false, message = "Reminder not found" });
                }

                var result = await _reminderService.DeleteReminderAsync(id, currentUser.Id);
                
                if (result)
                {
                    return Ok(new { success = true, message = "Reminder deleted successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to delete reminder" });
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error deleting reminder via chat API");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("reminders/{id}")]
        public async Task<IActionResult> GetReminderDetails(int id)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var reminder = await _reminderService.GetReminderByIdAsync(id);
                if (reminder == null || reminder.UserId != currentUser.Id)
                {
                    return NotFound(new { success = false, message = "Reminder not found" });
                }

                var reminderDetails = new
                {
                    id = reminder.ReminderId,
                    title = reminder.Name,
                    description = reminder.Message,
                    schedules = reminder.Schedules.Select(s => new
                    {
                        id = s.ScheduleId,
                        reminderTime = s.Date.Add(s.Time),
                        isReminderSent = s.IsReminderSent,
                        reminderMode = "Email" // Default mode
                    })
                };

                return Ok(new { success = true, reminder = reminderDetails });
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error getting reminder details via chat API");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }

    public class CreateReminderApiRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? ReminderTime { get; set; }
        public ReminderMode? ReminderMode { get; set; }
    }

    public class UpdateReminderApiRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
    }
} 