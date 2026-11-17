using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TalkToLiveChat.BusinessLogic;
using TalkToLiveChat.DataAccess.Repositories;
using TalkToLiveChat.Hubs;
using TalkToLiveChat.Models.ViewModels;

namespace TalkToLiveChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatWidgetController : ControllerBase
    {
        private readonly IServiceRepository _serviceRepo;
        private readonly IVisitorRepository _visitorRepo;
        private readonly IChatSessionRepository _sessionRepo;
        private readonly IMessageRepository _messageRepo;
        private readonly IChatAssignmentService _assignmentService;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatWidgetController(
            IServiceRepository serviceRepo,
            IVisitorRepository visitorRepo,
            IChatSessionRepository sessionRepo,
            IMessageRepository messageRepo,
            IChatAssignmentService assignmentService,
            IWebHostEnvironment env,
            IHubContext<ChatHub> hubContext)
        {
            _serviceRepo = serviceRepo;
            _visitorRepo = visitorRepo;
            _sessionRepo = sessionRepo;
            _messageRepo = messageRepo;
            _assignmentService = assignmentService;
            _env = env;
            _hubContext = hubContext;
        }

        // GET: api/chatwidget/services
        [HttpGet("services")]
        public async Task<IActionResult> GetActiveServices()
        {
            var services = await _serviceRepo.GetActiveServicesAsync();
            return Ok(new { success = true, data = services });
        }

        // POST: api/chatwidget/validate-mobile
        [HttpPost("validate-mobile")]
        public async Task<IActionResult> ValidateMobile([FromBody] MobileValidationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MobileNumber))
            {
                return BadRequest(new MobileValidationResponse
                {
                    Success = false,
                    Message = "Mobile number is required."
                });
            }

            string cleanedNumber = request.MobileNumber.Trim().Replace(" ", "").Replace("-", "");

            // Server-side validation: Must be a valid 10-digit Indian mobile number (starts with 6, 7, 8, or 9)
            if (!Regex.IsMatch(cleanedNumber, @"^[6-9]\d{9}$"))
            {
                return BadRequest(new MobileValidationResponse
                {
                    Success = false,
                    Message = "Please enter a valid 10-digit Indian mobile number."
                });
            }

            string countryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? "+91" : request.CountryCode.Trim();

            // Save or fetch visitor in SQL Server via ADO.NET
            var visitor = await _visitorRepo.FindOrCreateVisitorAsync(countryCode, cleanedNumber);

            return Ok(new MobileValidationResponse
            {
                Success = true,
                Message = "Mobile number validated successfully.",
                VisitorId = visitor.VisitorId,
                CountryCode = visitor.CountryCode,
                MobileNumber = visitor.MobileNumber
            });
        }

        // POST: api/chatwidget/start-chat
        [HttpPost("start-chat")]
        public async Task<IActionResult> StartChat([FromBody] StartChatRequest request)
        {
            if (request == null || request.VisitorId <= 0 || request.ServiceId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid visitor or service selected." });
            }

            var response = await _assignmentService.InitiateChatSessionAsync(request.VisitorId, request.ServiceId);

            // Notify all agents about new incoming chat session
            await _hubContext.Clients.Group("agents_room").SendAsync("NewIncomingChat", new
            {
                ChatSessionId = response.ChatSessionId,
                ServiceId = request.ServiceId,
                ServiceName = response.ServiceName,
                Status = response.SessionStatus,
                AssignedAgentId = response.AssignedAgentId,
                AgentName = response.AgentName,
                Timestamp = DateTime.UtcNow
            });

            return Ok(response);
        }

        // GET: api/chatwidget/session/{sessionId}
        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSessionDetails(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { success = false, message = "Session ID is required." });
            }

            var session = await _sessionRepo.GetChatSessionByIdAsync(sessionId.Trim());
            if (session == null)
            {
                return NotFound(new { success = false, message = "Chat session not found." });
            }

            var messages = await _messageRepo.GetChatMessagesAsync(sessionId.Trim());

            return Ok(new
            {
                success = true,
                session = session,
                messages = messages
            });
        }

        // POST: api/chatwidget/upload-attachment
        [HttpPost("upload-attachment")]
        public async Task<IActionResult> UploadAttachment([FromForm] IFormFile file, [FromForm] string chatSessionId, [FromForm] string senderId, [FromForm] string senderType, [FromForm] string senderName)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file uploaded." });
            }

            if (file.Length > 10 * 1024 * 1024) // 10MB limit
            {
                return BadRequest(new { success = false, message = "File size cannot exceed 10MB." });
            }

            string uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            string originalFileName = Path.GetFileName(file.FileName);
            string fileExt = Path.GetExtension(originalFileName).ToLowerInvariant();
            string uniqueFileName = $"{Guid.NewGuid():N}{fileExt}";
            string physicalPath = Path.Combine(uploadsDir, uniqueFileName);
            string virtualPath = $"/uploads/{uniqueFileName}";

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            bool isImage = fileExt is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp";
            string messageType = isImage ? "Image" : "File";
            string messageText = isImage ? $"[Image: {originalFileName}]" : $"[File: {originalFileName}]";

            // Save message and attachment
            var savedMessage = await _messageRepo.SaveMessageAsync(chatSessionId, senderId, senderType, messageText, messageType);
            if (savedMessage != null)
            {
                int attachmentId = await _messageRepo.SaveAttachmentAsync(savedMessage.MessageId, originalFileName, virtualPath, file.ContentType);
                savedMessage.AttachmentId = attachmentId;
                savedMessage.FileName = originalFileName;
                savedMessage.FilePath = virtualPath;
                savedMessage.ContentType = file.ContentType;

                // Broadcast via SignalR
                var broadcastPayload = new
                {
                    MessageId = savedMessage.MessageId,
                    ChatSessionId = chatSessionId,
                    SenderId = senderId,
                    SenderName = senderName,
                    SenderType = senderType,
                    MessageText = messageText,
                    MessageType = messageType,
                    SentDate = savedMessage.SentDate,
                    IsRead = false,
                    FileName = originalFileName,
                    FilePath = virtualPath,
                    ContentType = file.ContentType
                };

                string groupName = $"session_{chatSessionId.Trim()}";
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", broadcastPayload);
                await _hubContext.Clients.Group("agents_room").SendAsync("NewActivity", new
                {
                    ChatSessionId = chatSessionId,
                    SenderType = senderType,
                    SenderName = senderName,
                    MessagePreview = messageText,
                    SentDate = DateTime.UtcNow
                });

                return Ok(new { success = true, message = savedMessage });
            }

            return StatusCode(500, new { success = false, message = "Failed to save attachment." });
        }

        // POST: api/chatwidget/rate-chat
        [HttpPost("rate-chat")]
        public async Task<IActionResult> RateChat([FromBody] RatingSubmissionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.ChatSessionId) || dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new { success = false, message = "Invalid rating submission." });
            }

            int ratingId = await _sessionRepo.SaveRatingAsync(dto.ChatSessionId, dto.Rating, dto.Feedback);

            return Ok(new { success = true, ratingId = ratingId, message = "Thank you for your feedback!" });
        }
    }
}
