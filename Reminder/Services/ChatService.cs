using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Reminder.Models;
using Reminder.Models.DBEntities;

namespace Reminder.Services
{
    public class ChatService : IChatService
    {
        private readonly IReminderService _reminderService;
        private readonly ILoggingService _loggingService;
        private readonly HttpClient _httpClient;
        private readonly string _groqApiKey;
        private readonly string _groqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
        private readonly SchedulerDbContext _context; // Add database context for conversation history

        public ChatService(
            IReminderService reminderService,
            ILoggingService loggingService,
            HttpClient httpClient,
            SchedulerDbContext context) // Add context parameter
        {
            _reminderService = reminderService;
            _loggingService = loggingService;
            _httpClient = httpClient;
            _context = context; // Store context
            _groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";
        }

        public async Task<ChatResponse> ProcessUserInputAsync(string userId, string userInput)
        {
            try
            {
                // Get conversation history for context
                var conversationHistory = await GetConversationHistoryAsync(userId, 10); // Get last 10 messages
                
                // Create system prompt with security context
                var systemPrompt = CreateSystemPrompt(userId);

                // Prepare messages for Groq API with conversation history
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                // Add conversation history as context
                foreach (var message in conversationHistory)
                {
                    if (message.IsUserMessage)
                    {
                        messages.Add(new { role = "user", content = message.Message });
                    }
                    else
                    {
                        messages.Add(new { role = "assistant", content = message.Response });
                    }
                }

                // Add current user input
                messages.Add(new { role = "user", content = userInput });

                // Call Groq API
                var response = await CallGroqApiAsync(messages);
                
                // Process the response and extract intent
                var processedResponse = await ProcessAIResponseAsync(userId, response, userInput);

                // Store the conversation in database
                await StoreConversationAsync(userId, userInput, processedResponse.Message, processedResponse.Intent, processedResponse.ExtractedData);

                return processedResponse;
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error processing user input for user {UserId}", userId);
                return new ChatResponse
                {
                    Message = "I'm sorry, I encountered an error processing your request. Please try again.",
                    Success = false
                };
            }
        }

        private async Task<List<ChatMessage>> GetConversationHistoryAsync(string userId, int messageCount)
        {
            try
            {
                return await _context.ChatMessages
                    .Where(cm => cm.UserId == userId)
                    .OrderByDescending(cm => cm.Timestamp)
                    .Take(messageCount)
                    .OrderBy(cm => cm.Timestamp) // Reorder chronologically for context
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error retrieving conversation history for user {UserId}", userId);
                return new List<ChatMessage>();
            }
        }

        private async Task StoreConversationAsync(string userId, string userMessage, string aiResponse, string? intent, object? extractedData)
        {
            try
            {
                // Store user message
                var userChatMessage = new ChatMessage
                {
                    UserId = userId,
                    Message = userMessage,
                    Response = "", // User messages don't have responses
                    IsUserMessage = true,
                    Intent = intent,
                    ExtractedData = extractedData != null ? JsonSerializer.Serialize(extractedData) : null,
                    Timestamp = DateTime.UtcNow
                };

                // Store AI response
                var aiChatMessage = new ChatMessage
                {
                    UserId = userId,
                    Message = "", // AI responses don't have user messages
                    Response = aiResponse,
                    IsUserMessage = false,
                    Intent = intent,
                    ExtractedData = extractedData != null ? JsonSerializer.Serialize(extractedData) : null,
                    Timestamp = DateTime.UtcNow
                };

                _context.ChatMessages.Add(userChatMessage);
                _context.ChatMessages.Add(aiChatMessage);
                await _context.SaveChangesAsync();

                _loggingService.LogInformation("Stored conversation for user {UserId}: User message and AI response", userId);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error storing conversation for user {UserId}", userId);
            }
        }

        private string CreateSystemPrompt(string userId)
        {
            try
            {
                var instructionsPath = Path.Combine(Directory.GetCurrentDirectory(), "Models", "ReminderInstructions.txt");
                if (File.Exists(instructionsPath))
                {
                    return File.ReadAllText(instructionsPath);
                }
                else
                {
                    _loggingService.LogWarning("ReminderInstructions.txt not found at {Path}, using fallback", instructionsPath);
                    return GetFallbackInstructions();
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error reading ReminderInstructions.txt, using fallback");
                return GetFallbackInstructions();
            }
        }

        private static string GetFallbackInstructions()
        {
            return @"You are an AI assistant for a reminder management system. You help users manage their reminders through natural language.

                SECURITY RULES:
                - You can only access data for the current authenticated user
                - Never expose sensitive information like passwords, API keys, or personal details
                - Always validate user permissions before performing actions
                - If asked about other users' data, politely decline

                RESPONSE RULES:
                - Never mention API endpoints, backend actions, or technical details in your response.
                - Only show the user's actual reminders in a friendly, concise way.
                - Do not hallucinate or invent reminders; only display real reminders provided to you.
                - If there are no reminders, say so politely.

                Remember to be helpful, secure, and only access data for the current user through the provided API endpoints.";
        }

        private async Task<string> CallGroqApiAsync(List<object> messages)
        {
            if (string.IsNullOrEmpty(_groqApiKey))
            {
                throw new InvalidOperationException("GROQ_API_KEY environment variable is not set");
            }

            var requestBody = new
            {
                model = "llama3-8b-8192",
                messages = messages,
                max_tokens = 1000,
                temperature = 0.7
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_groqApiKey}");

            var response = await _httpClient.PostAsync(_groqApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _loggingService.LogError("Groq API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new Exception($"Groq API error: {response.StatusCode}");
            }

            var groqResponse = JsonSerializer.Deserialize<GroqResponse>(responseContent);
            return groqResponse?.choices?.FirstOrDefault()?.message?.content ?? "I'm sorry, I couldn't process your request.";
        }

        private async Task<ChatResponse> ProcessAIResponseAsync(string userId, string aiResponse, string userInput)
        {
            // Try to parse LLM response as JSON for intent extraction and suggested response
            try
            {
                _loggingService.LogInformation("Raw LLM response: {Response}", aiResponse);
                using var doc = JsonDocument.Parse(aiResponse);
                var intent = doc.RootElement.GetProperty("intent").GetString();
                JsonElement? parameters = null;
                if (doc.RootElement.TryGetProperty("params", out var paramsElement))
                {
                    parameters = paramsElement;
                }
                string suggestedResponse = doc.RootElement.TryGetProperty("response", out var respElement)
                    ? respElement.GetString() ?? string.Empty
                    : string.Empty;
                _loggingService.LogInformation("Extracted intent: {Intent}, params: {Params}, suggested response: {Response}", intent, parameters?.ToString(), suggestedResponse);

                // Convert JsonElement parameters to Dictionary<string, object> for handler methods
                Dictionary<string, object>? convertedParams = null;
                if (parameters.HasValue)
                {
                    convertedParams = new Dictionary<string, object>();
                    foreach (var property in parameters.Value.EnumerateObject())
                    {
                        convertedParams[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString() ?? "",
                            JsonValueKind.Number => property.Value.GetInt32(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => property.Value.GetString() ?? ""
                        };
                    }
                }

                switch (intent)
                {
                    case "CreateReminder":
                        {
                            var result = await HandleScheduleReminderAsync(userId, convertedParams, aiResponse);
                            result.Message = result.Success ? suggestedResponse : "Sorry, I couldn't create your reminder. Please try again.";
                            return result;
                        }
                    case "DeleteReminder":
                        {
                            var result = await HandleDeleteReminderAsync(userId, convertedParams, aiResponse);
                            result.Message = result.Success ? suggestedResponse : "Sorry, I couldn't delete your reminder. Please try again.";
                            return result;
                        }
                    case "ListReminders":
                        {
                            var result = await HandleListRemindersAsync(userId, aiResponse);
                            // If reminders found, use backend result; else, fallback to LLM response
                            result.Message = !string.IsNullOrWhiteSpace(result.Message) ? result.Message : suggestedResponse;
                            return result;
                        }
                    case "EditReminder":
                        {
                            var result = await HandleEditReminderAsync(userId, convertedParams, aiResponse);
                            result.Message = result.Success ? suggestedResponse : "Sorry, I couldn't update your reminder. Please try again.";
                            return result;
                        }
                    case "FollowUpResponse":
                        {
                            var result = await HandleFollowUpResponseAsync(userId, convertedParams, aiResponse);
                            result.Message = result.Success ? suggestedResponse : "I'm sorry, I couldn't process your response. Please try again.";
                            return result;
                        }
                    case "Clarification":
                        {
                            return new ChatResponse
                            {
                                Message = suggestedResponse,
                                Intent = "clarification",
                                Success = true,
                                RequiresFollowUp = true
                            };
                        }
                    default:
                        _loggingService.LogWarning("Unknown intent from LLM: {Intent}", intent);
                        break;
                }
            }
            catch (JsonException jsonEx)
            {
                _loggingService.LogWarning("Failed to parse LLM response as JSON. Falling back to regex/old logic. Exception: {ExceptionMessage}", jsonEx.Message);
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error in LLM intent extraction.");
            }

            // Fallback: old regex/keyword logic
            var intents = ExtractIntents(userInput, aiResponse);
            var extractedData = ExtractData(userInput, aiResponse);
            var primaryIntent = intents.FirstOrDefault();
            switch (primaryIntent)
            {
                case "list_reminders":
                    return await HandleListRemindersAsync(userId, aiResponse);
                case "schedule_reminder":
                    return await HandleScheduleReminderAsync(userId, extractedData, aiResponse);
                case "edit_reminder":
                    return await HandleEditReminderAsync(userId, extractedData, aiResponse);
                case "delete_reminder":
                    return await HandleDeleteReminderAsync(userId, extractedData, aiResponse);
                case "get_reminder_details":
                    return await HandleGetReminderDetailsAsync(userId, extractedData, aiResponse);
                case "general":
                    return await HandleGeneralConversationAsync(userId, aiResponse);
                default:
                    return new ChatResponse
                    {
                        Message = aiResponse,
                        Intent = primaryIntent ?? "general",
                        ExtractedData = extractedData
                    };
            }
        }

        private List<string> ExtractIntents(string userInput, string aiResponse)
        {
            var intents = new List<string>();
            var input = userInput.ToLower();
            
            // More precise intent detection with context awareness
            
            // LIST REMINDERS - Look for specific phrases that indicate listing
            if (Regex.IsMatch(input, @"\b(?:what|show|list|check|display|see|view|do i have|are there)\s+(?:reminders?|my reminders?|scheduled reminders?)", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(input, @"\b(?:reminders?|reminder list)\s+(?:do i have|what|show|list)", RegexOptions.IgnoreCase) ||
                input.Contains("what reminders") || input.Contains("show my reminders") || input.Contains("list my reminders") ||
                input.Contains("check my reminders") || input.Contains("do i have any reminders"))
            {
                intents.Add("list_reminders");
            }
            
            // SCHEDULE REMINDER - Look for explicit creation phrases
            else if (Regex.IsMatch(input, @"\b(?:remind me to|schedule|create|set|add)\s+(?:a\s+)?(?:reminder|meeting|appointment|call|task)", RegexOptions.IgnoreCase) ||
                     Regex.IsMatch(input, @"\b(?:reminder|meeting|appointment|call|task)\s+(?:for|to|at)", RegexOptions.IgnoreCase) ||
                     input.Contains("remind me to") || input.Contains("schedule a") || input.Contains("create a reminder") ||
                     input.Contains("set a reminder") || input.Contains("add a reminder"))
            {
                intents.Add("schedule_reminder");
            }
            
            // EDIT REMINDER - Look for modification phrases
            else if (Regex.IsMatch(input, @"\b(?:edit|change|update|modify|move|reschedule)\s+(?:my|the|this)\s+(?:reminder|meeting|appointment)", RegexOptions.IgnoreCase) ||
                     input.Contains("edit my") || input.Contains("change my") || input.Contains("update my") ||
                     input.Contains("modify my") || input.Contains("move my"))
            {
                intents.Add("edit_reminder");
            }
            
            // DELETE REMINDER - Look for removal phrases
            else if (Regex.IsMatch(input, @"\b(?:delete|remove|cancel|drop)\s+(?:my|the|this)\s+(?:reminder|meeting|appointment)", RegexOptions.IgnoreCase) ||
                     input.Contains("delete my") || input.Contains("remove my") || input.Contains("cancel my") ||
                     input.Contains("drop my"))
            {
                intents.Add("delete_reminder");
            }
            
            // GET REMINDER DETAILS - Look for specific information requests
            else if (Regex.IsMatch(input, @"\b(?:tell me|what's|what is|details|info|information)\s+(?:about|for|on)\s+(?:my|the|this)", RegexOptions.IgnoreCase) ||
                     input.Contains("tell me about") || input.Contains("what's the info") || input.Contains("details about"))
            {
                intents.Add("get_reminder_details");
            }
            
            // GENERAL - For greetings, help, and general conversation
            else if (Regex.IsMatch(input, @"\b(?:hello|hi|hey|how are you|what can you do|help|thanks|thank you)", RegexOptions.IgnoreCase) ||
                     input.Length < 10) // Short inputs are likely general
            {
                intents.Add("general");
            }
            
            // If no specific intent found, return general
            if (!intents.Any())
                intents.Add("general");
            
            return intents;
        }

        private object? ExtractData(string userInput, string aiResponse)
        {
            var data = new Dictionary<string, object>();
            
            // Extract title/description with better patterns
            var titlePatterns = new[]
            {
                @"(?:remind me to|schedule|create reminder for)\s+(.+?)(?:\s+(?:at|on|for|tomorrow|next|in)|$)",
                @"(?:meeting with|call|email|text)\s+(.+?)(?:\s+(?:at|on|for|tomorrow|next|in)|$)",
                @"(?:appointment|meeting|task)\s+(?:with\s+)?(.+?)(?:\s+(?:at|on|for|tomorrow|next|in)|$)"
            };
            
            foreach (var pattern in titlePatterns)
            {
                var titleMatch = Regex.Match(userInput, pattern, RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    var title = titleMatch.Groups[1].Value.Trim();
                    // Clean up the title
                    title = Regex.Replace(title, @"\s+(?:at|on|for|tomorrow|next|in)\s+.*$", "", RegexOptions.IgnoreCase);
                    data["title"] = title;
                    data["message"] = title; // Use same text for message
                    break;
                }
            }
            
            // If no title found, try to extract from the beginning
            if (!data.ContainsKey("title"))
            {
                var words = userInput.Split(' ');
                var titleWords = words.Take(3).ToArray(); // Take first 3 words as title
                if (titleWords.Length > 0)
                {
                    var title = string.Join(" ", titleWords);
                    data["title"] = title;
                    data["message"] = title;
                }
            }
            
            // Extract date/time patterns with improved matching
            var dateTimePatterns = new[]
            {
                @"(?:at|on|for)\s+(.+?)(?:\s|$)",
                @"(?:tomorrow|today)\s+(?:at\s+)?(\d{1,2}(?::\d{2})?\s*(?:am|pm)?)",
                @"(?:next\s+)?(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+(?:at\s+)?(\d{1,2}(?::\d{2})?\s*(?:am|pm)?)",
                @"in\s+(\d+)\s+(?:hours?|days?)",
                @"(\d{1,2}:\d{2}\s*(?:am|pm)?)",
                @"(\d{1,2}(?::\d{2})?\s*(?:am|pm)?)"
            };
            
            foreach (var pattern in dateTimePatterns)
            {
                var match = Regex.Match(userInput, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    data["datetime"] = match.Groups[1].Value.Trim();
                    break;
                }
            }
            
            // Extract priority with more patterns
            if (Regex.IsMatch(userInput, @"(?:high|urgent|critical|important)", RegexOptions.IgnoreCase))
                data["priority"] = "High";
            else if (Regex.IsMatch(userInput, @"(?:medium|normal|moderate)", RegexOptions.IgnoreCase))
                data["priority"] = "Normal";
            else
                data["priority"] = "Low"; // Default
            
            // Extract mode with more patterns
            if (Regex.IsMatch(userInput, @"(?:sms|text|message|whatsapp)", RegexOptions.IgnoreCase))
                data["mode"] = "SMS";
            else if (Regex.IsMatch(userInput, @"(?:call|phone|ring)", RegexOptions.IgnoreCase))
                data["mode"] = "Call";
            else
                data["mode"] = "Email"; // Default
            
            return data.Any() ? data : null;
        }

        private async Task<ChatResponse> HandleListRemindersAsync(string userId, string aiResponse)
        {
            try
            {
                // Get reminders directly from service
                var reminders = await _reminderService.GetUserRemindersAsync(userId);
                
                if (!reminders.Any())
                {
                    return new ChatResponse
                    {
                        Message = "You don't have any reminders scheduled at the moment.",
                        Intent = "list_reminders"
                    };
                }

                var reminderList = string.Join("\n", reminders.Select(r => 
                {
                    var schedule = r.Schedules?.FirstOrDefault();
                    var scheduleText = schedule != null 
                        ? $" (Due: {schedule.Date.Add(schedule.Time):MMM dd, yyyy HH:mm})"
                        : " (No schedule set)";
                    return $"• ID: {r.ReminderId} | Title: {r.Name} | Message: {r.Message}{scheduleText}";
                }));
                
                return new ChatResponse
                {
                    Message = $"Here are your reminders:\n\n{reminderList}",
                    Intent = "list_reminders"
                };
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error listing reminders for user {UserId}", userId);
                return new ChatResponse
                {
                    Message = "I'm sorry, I couldn't retrieve your reminders. Please try again.",
                    Intent = "list_reminders",
                    Success = false
                };
            }
        }

        private async Task<ChatResponse> HandleScheduleReminderAsync(string userId, object? extractedData, string aiResponse)
        {
            try
            {
                if (extractedData is Dictionary<string, object> data)
                {
                    _loggingService.LogInformation("Processing reminder creation with data: {Data}", string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}")));
                    
                    // Handle LLM parameters (task, time) and convert to expected format
                    var title = "";
                    var message = "";
                    var reminderTime = (DateTime?)null;
                    
                    // Extract task from LLM parameters
                    if (data.ContainsKey("task"))
                    {
                        title = data["task"].ToString() ?? "Reminder";
                        message = title;
                    }
                    else if (data.ContainsKey("title"))
                    {
                        title = data["title"].ToString() ?? "Reminder";
                        message = data.ContainsKey("message") ? data["message"].ToString() ?? title : title;
                    }
                    
                    // Extract time from LLM parameters
                    if (data.ContainsKey("time"))
                    {
                        reminderTime = ParseDateTime(data["time"].ToString() ?? "");
                    }
                    else if (data.ContainsKey("datetime"))
                    {
                        reminderTime = ParseDateTime(data["datetime"].ToString() ?? "");
                    }
                    
                    // Get other parameters
                    var priority = data.ContainsKey("priority") ? data["priority"].ToString() : "Low";
                    var mode = data.ContainsKey("mode") ? data["mode"].ToString() : "Email";
                    
                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return new ChatResponse
                        {
                            Message = "I need to know what you'd like to be reminded about. Please provide a task or title.",
                            Intent = "schedule_reminder",
                            Success = false,
                            RequiresFollowUp = true,
                            FollowUpQuestion = "What would you like to be reminded about?"
                        };
                    }
                    
                    // Create reminder directly via service
                    var reminderRequest = new ReminderViewModel
                    {
                        Name = title,
                        Message = message,
                        UserId = userId,
                        ImportanceLevel = priority,
                        IsEmailModeSelected = mode.Equals("Email", StringComparison.OrdinalIgnoreCase),
                        IsTextModeSelected = mode.Equals("SMS", StringComparison.OrdinalIgnoreCase),
                        IsCallModeSelected = mode.Equals("Call", StringComparison.OrdinalIgnoreCase)
                    };
                    
                    // Add schedule if time is provided
                    if (reminderTime.HasValue)
                    {
                        reminderRequest.Schedules = new List<ScheduleViewModel>
                        {
                            new ScheduleViewModel
                            {
                                Date = reminderTime.Value.Date,
                                Time = reminderTime.Value.TimeOfDay
                            }
                        };
                    }
                    
                    var result = await _reminderService.CreateReminderAsync(reminderRequest);
                    
                    if (result)
                    {
                        var timeInfo = reminderTime.HasValue ? $" for {reminderTime.Value:MMM dd, yyyy 'at' h:mm tt}" : "";
                        return new ChatResponse
                        {
                            Message = $"✅ Reminder '{title}' has been scheduled successfully{timeInfo}!",
                            Intent = "schedule_reminder",
                            Success = true
                        };
                    }
                    
                    return new ChatResponse
                    {
                        Message = "I'm sorry, I couldn't schedule the reminder. Please try again.",
                        Intent = "schedule_reminder",
                        Success = false
                    };
                }
                
                return new ChatResponse
                {
                    Message = aiResponse,
                    Intent = "schedule_reminder",
                    RequiresFollowUp = true,
                    FollowUpQuestion = "Please provide the reminder title and when you'd like to be reminded.",
                    ExtractedData = extractedData
                };
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error scheduling reminder for user {UserId}", userId);
                return new ChatResponse
                {
                    Message = "I'm sorry, I encountered an error scheduling your reminder. Please try again.",
                    Intent = "schedule_reminder",
                    Success = false
                };
            }
        }

        private async Task<ChatResponse> HandleEditReminderAsync(string userId, object? extractedData, string aiResponse)
        {
            return new ChatResponse
            {
                Message = aiResponse,
                Intent = "edit_reminder",
                RequiresFollowUp = true,
                FollowUpQuestion = "Which reminder would you like to edit? Please specify the reminder title or ID.",
                ExtractedData = extractedData
            };
        }

        private async Task<ChatResponse> HandleDeleteReminderAsync(string userId, object? extractedData, string aiResponse)
        {
            try
            {
                string? targetTitle = null;
                int? targetId = null;

                // Try to extract title or ID from the request
                if (extractedData is Dictionary<string, object> data)
                {
                    _loggingService.LogInformation("Processing delete reminder with data: {Data}", string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}")));
                    
                    if (data.ContainsKey("task"))
                    {
                        targetTitle = data["task"].ToString();
                    }
                    else if (data.ContainsKey("title"))
                    {
                        targetTitle = data["title"].ToString();
                    }
                    else if (data.ContainsKey("id") && int.TryParse(data["id"].ToString(), out var id))
                    {
                        targetId = id;
                    }
                }

                // If we have a title or ID, try to delete directly
                if (!string.IsNullOrWhiteSpace(targetTitle) || targetId.HasValue)
                {
                    var result = await _reminderService.DeleteReminderAsync(userId, targetTitle, targetId);
                    
                    if (result)
                    {
                        var target = targetId.HasValue ? $"ID {targetId}" : $"'{targetTitle}'";
                        return new ChatResponse
                        {
                            Message = $"✅ Reminder {target} has been deleted successfully!",
                            Intent = "delete_reminder",
                            Success = true
                        };
                    }
                    else
                    {
                        var target = targetId.HasValue ? $"ID {targetId}" : $"'{targetTitle}'";
                        return new ChatResponse
                        {
                            Message = $"❌ Reminder {target} not found or couldn't be deleted.",
                            Intent = "delete_reminder",
                            Success = false
                        };
                    }
                }

                // If no title/ID provided, show available reminders with IDs
                var reminders = await _reminderService.GetUserRemindersAsync(userId);
                
                if (!reminders.Any())
                {
                    return new ChatResponse
                    {
                        Message = "You don't have any reminders to delete.",
                        Intent = "delete_reminder",
                        Success = false
                    };
                }

                var reminderList = string.Join("\n", reminders.Select(r => $"• ID: {r.ReminderId} | Title: {r.Name} | Message: {r.Message}"));
                
                return new ChatResponse
                {
                    Message = $"Please specify which reminder to delete by ID or title:\n\n{reminderList}",
                    Intent = "delete_reminder",
                    Success = false,
                    RequiresFollowUp = true,
                    FollowUpQuestion = "Enter the reminder ID or title to delete:"
                };
            }
            catch (Exception ex)
            {
                _loggingService.LogError(ex, "Error in delete reminder for user {UserId}", userId);
                return new ChatResponse
                {
                    Message = "I'm sorry, I encountered an error processing your delete request. Please try again.",
                    Intent = "delete_reminder",
                    Success = false
                };
            }
        }

        private async Task<ChatResponse> HandleGetReminderDetailsAsync(string userId, object? extractedData, string aiResponse)
        {
            return new ChatResponse
            {
                Message = aiResponse,
                Intent = "get_reminder_details",
                RequiresFollowUp = true,
                FollowUpQuestion = "Which reminder would you like details for? Please specify the reminder title or ID.",
                ExtractedData = extractedData
            };
        }

        private async Task<ChatResponse> HandleGeneralConversationAsync(string userId, string aiResponse)
        {
            // For general conversation, just return the AI response as-is
            return new ChatResponse
            {
                Message = aiResponse,
                Intent = "general",
                Success = true
            };
        }

        private async Task<ChatResponse> HandleFollowUpResponseAsync(string userId, object? extractedData, string aiResponse)
        {
            if (extractedData is Dictionary<string, object> data)
            {
                _loggingService.LogInformation("Processing follow-up response with data: {Data}", string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}")));

                var message = "";
                var reminderTime = (DateTime?)null;

                if (data.ContainsKey("message"))
                {
                    message = data["message"].ToString() ?? "";
                }
                else if (data.ContainsKey("title"))
                {
                    message = data["title"].ToString() ?? "";
                }

                if (data.ContainsKey("datetime"))
                {
                    reminderTime = ParseDateTime(data["datetime"].ToString() ?? "");
                }
                else if (data.ContainsKey("time"))
                {
                    reminderTime = ParseDateTime(data["time"].ToString() ?? "");
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    return new ChatResponse
                    {
                        Message = "I need to know what you'd like to be reminded about. Please provide a task or title.",
                        Intent = "followup_response",
                        Success = false,
                        RequiresFollowUp = true,
                        FollowUpQuestion = "What would you like to be reminded about?"
                    };
                }

                var reminderRequest = new ReminderViewModel
                {
                    Name = message,
                    Message = message,
                    UserId = userId,
                    ImportanceLevel = "Low", // Default for follow-up
                    IsEmailModeSelected = false,
                    IsTextModeSelected = false,
                    IsCallModeSelected = false
                };

                if (reminderTime.HasValue)
                {
                    reminderRequest.Schedules = new List<ScheduleViewModel>
                    {
                        new ScheduleViewModel
                        {
                            Date = reminderTime.Value.Date,
                            Time = reminderTime.Value.TimeOfDay
                        }
                    };
                }

                var result = await _reminderService.CreateReminderAsync(reminderRequest);

                if (result)
                {
                    var timeInfo = reminderTime.HasValue ? $" for {reminderTime.Value:MMM dd, yyyy 'at' h:mm tt}" : "";
                    return new ChatResponse
                    {
                        Message = $"✅ Reminder '{message}' has been scheduled successfully{timeInfo}!",
                        Intent = "followup_response",
                        Success = true
                    };
                }
                else
                {
                    return new ChatResponse
                    {
                        Message = "I'm sorry, I couldn't schedule the reminder. Please try again.",
                        Intent = "followup_response",
                        Success = false
                    };
                }
            }
            return new ChatResponse
            {
                Message = aiResponse,
                Intent = "followup_response",
                RequiresFollowUp = true,
                FollowUpQuestion = "Please provide the reminder title and when you'd like to be reminded.",
                ExtractedData = extractedData
            };
        }

        // Chat history is now session-based, not stored in database
        
        private DateTime? ParseDateTime(string dateTimeString)
        {
            try
            {
                var now = DateTime.Now;
                var input = dateTimeString.ToLower().Trim();
                
                // Handle "tomorrow at 3pm" or "tomorrow 3pm"
                if (input.StartsWith("tomorrow"))
                {
                    var timePart = input.Replace("tomorrow", "").Trim();
                    if (timePart.StartsWith("at ")) timePart = timePart.Substring(3);
                    
                    var time = ParseTime(timePart);
                    if (time.HasValue)
                    {
                        return now.AddDays(1).Date.Add(time.Value);
                    }
                }
                
                // Handle "next monday at 10am"
                if (input.StartsWith("next "))
                {
                    var dayPart = input.Substring(5).Split(' ')[0];
                    var timePart = input.Contains(" at ") ? input.Split(" at ")[1] : "9:00am";
                    
                    var dayOfWeek = ParseDayOfWeek(dayPart);
                    var time = ParseTime(timePart);
                    
                    if (dayOfWeek.HasValue && time.HasValue)
                    {
                        var nextDay = GetNextDayOfWeek(dayOfWeek.Value);
                        return nextDay.Date.Add(time.Value);
                    }
                }
                
                // Handle "in 2 hours" or "in 3 days"
                if (input.StartsWith("in "))
                {
                    var parts = input.Split(' ');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out var amount))
                    {
                        var unit = parts[2].ToLower();
                        if (unit.StartsWith("hour"))
                            return now.AddHours(amount);
                        else if (unit.StartsWith("day"))
                            return now.AddDays(amount);
                    }
                }
                
                // Handle simple time like "3pm" or "15:30"
                var simpleTime = ParseTime(input);
                if (simpleTime.HasValue)
                {
                    return now.Date.Add(simpleTime.Value);
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private TimeSpan? ParseTime(string timeString)
        {
            try
            {
                var input = timeString.ToLower().Trim();
                
                // Handle "3pm", "3:30pm"
                if (input.Contains("pm") || input.Contains("am"))
                {
                    var timePart = input.Replace("pm", "").Replace("am", "").Trim();
                    var isPM = input.Contains("pm");
                    
                    if (timePart.Contains(":"))
                    {
                        var parts = timePart.Split(':');
                        if (int.TryParse(parts[0], out var hour) && int.TryParse(parts[1], out var minute))
                        {
                            if (isPM && hour != 12) hour += 12;
                            if (!isPM && hour == 12) hour = 0;
                            return new TimeSpan(hour, minute, 0);
                        }
                    }
                    else
                    {
                        if (int.TryParse(timePart, out var hour))
                        {
                            if (isPM && hour != 12) hour += 12;
                            if (!isPM && hour == 12) hour = 0;
                            return new TimeSpan(hour, 0, 0);
                        }
                    }
                }
                
                // Handle "15:30" format
                if (input.Contains(":"))
                {
                    var parts = input.Split(':');
                    if (int.TryParse(parts[0], out var hour) && int.TryParse(parts[1], out var minute))
                    {
                        return new TimeSpan(hour, minute, 0);
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private DayOfWeek? ParseDayOfWeek(string dayString)
        {
            return dayString.ToLower() switch
            {
                "monday" => DayOfWeek.Monday,
                "tuesday" => DayOfWeek.Tuesday,
                "wednesday" => DayOfWeek.Wednesday,
                "thursday" => DayOfWeek.Thursday,
                "friday" => DayOfWeek.Friday,
                "saturday" => DayOfWeek.Saturday,
                "sunday" => DayOfWeek.Sunday,
                _ => null
            };
        }
        
        private DateTime GetNextDayOfWeek(DayOfWeek dayOfWeek)
        {
            var today = DateTime.Today;
            var daysUntilTarget = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysUntilTarget);
        }
    }

    // Groq API response models
    public class GroqResponse
    {
        public List<GroqChoice>? choices { get; set; }
    }

    public class GroqChoice
    {
        public GroqMessage? message { get; set; }
    }

    public class GroqMessage
    {
        public string? content { get; set; }
    }
} 