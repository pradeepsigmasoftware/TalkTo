using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TalkToLiveChat.BusinessLogic;
using TalkToLiveChat.DataAccess.Repositories;
using TalkToLiveChat.Hubs;
using TalkToLiveChat.Models.ViewModels;

namespace TalkToLiveChat.Controllers
{
    [Authorize(Roles = "Agent,Admin")]
    public class AgentController : Controller
    {
        private readonly IAgentRepository _agentRepo;
        private readonly IChatSessionRepository _sessionRepo;
        private readonly IMessageRepository _messageRepo;
        private readonly IInternalNoteRepository _noteRepo;
        private readonly IChatAssignmentService _assignmentService;
        private readonly IHubContext<ChatHub> _hubContext;

        public AgentController(
            IAgentRepository agentRepo,
            IChatSessionRepository sessionRepo,
            IMessageRepository messageRepo,
            IInternalNoteRepository noteRepo,
            IChatAssignmentService assignmentService,
            IHubContext<ChatHub> hubContext)
        {
            _agentRepo = agentRepo;
            _sessionRepo = sessionRepo;
            _messageRepo = messageRepo;
            _noteRepo = noteRepo;
            _assignmentService = assignmentService;
            _hubContext = hubContext;
        }

        private int CurrentAgentId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;
        private string CurrentAgentName => User.FindFirstValue(ClaimTypes.Name) ?? "Support Agent";

        public async Task<IActionResult> Index()
        {
            var agent = await _agentRepo.GetAgentByIdAsync(CurrentAgentId);
            ViewBag.CurrentAgent = agent;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _agentRepo.GetAgentDashboardStatsAsync(CurrentAgentId);
            return Ok(stats);
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions()
        {
            // Fetch chats assigned to this agent (Active, Completed) + all Pending chats across the system
            var myActiveChats = await _sessionRepo.GetChatSessionsListAsync(CurrentAgentId, null, "Active", null, null, null);
            var myCompletedChats = await _sessionRepo.GetChatSessionsListAsync(CurrentAgentId, null, "Completed", null, null, null);
            var pendingChats = await _sessionRepo.GetChatSessionsListAsync(null, null, "Pending", null, null, null);

            return Ok(new
            {
                active = myActiveChats,
                pending = pendingChats,
                completed = myCompletedChats
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetSessionDetails(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest();

            var session = await _sessionRepo.GetChatSessionByIdAsync(sessionId.Trim());
            if (session == null) return NotFound();

            var messages = await _messageRepo.GetChatMessagesAsync(sessionId.Trim());
            var notes = await _noteRepo.GetInternalNotesAsync(sessionId.Trim());

            // Mark customer messages as read since agent opened the chat
            await _messageRepo.MarkMessagesAsReadAsync(sessionId.Trim(), "Agent");

            // Broadcast read receipt
            string groupName = $"session_{sessionId.Trim()}";
            await _hubContext.Clients.Group(groupName).SendAsync("MessagesRead", new
            {
                ChatSessionId = sessionId.Trim(),
                ReaderType = "Agent",
                ReadDate = DateTime.UtcNow
            });

            return Ok(new
            {
                session = session,
                messages = messages,
                notes = notes
            });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptChat([FromBody] string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest();

            var session = await _sessionRepo.AssignAgentAsync(sessionId.Trim(), CurrentAgentId, CurrentAgentName);

            // Send system message
            string connectMsg = $"{CurrentAgentName} has accepted and joined this chat.";
            await _messageRepo.SaveMessageAsync(sessionId.Trim(), "System", "System", connectMsg, "Text");

            // Broadcast to session & agents
            string groupName = $"session_{sessionId.Trim()}";
            await _hubContext.Clients.Group(groupName).SendAsync("AgentAssigned", new
            {
                ChatSessionId = sessionId.Trim(),
                AgentId = CurrentAgentId,
                AgentName = CurrentAgentName,
                SessionStatus = "Active"
            });

            await _hubContext.Clients.Group("agents_room").SendAsync("ChatStatusChanged", new
            {
                ChatSessionId = sessionId.Trim(),
                AgentId = CurrentAgentId,
                AgentName = CurrentAgentName,
                Status = "Active"
            });

            return Ok(new { success = true, session = session });
        }

        [HttpPost]
        public async Task<IActionResult> CloseSession([FromBody] string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest();

            var session = await _sessionRepo.CloseChatSessionAsync(sessionId.Trim(), CurrentAgentName);

            string groupName = $"session_{sessionId.Trim()}";
            await _hubContext.Clients.Group(groupName).SendAsync("ChatClosed", new
            {
                ChatSessionId = sessionId.Trim(),
                ClosedBy = CurrentAgentName,
                ClosedDate = DateTime.UtcNow
            });

            await _hubContext.Clients.Group("agents_room").SendAsync("ChatClosedNotice", new
            {
                ChatSessionId = sessionId.Trim(),
                ClosedBy = CurrentAgentName
            });

            return Ok(new { success = true, session = session });
        }

        [HttpGet]
        public async Task<IActionResult> GetTransferAgents()
        {
            var agents = await _agentRepo.GetAvailableAgentsForTransferAsync(CurrentAgentId);
            return Ok(agents);
        }

        [HttpPost]
        public async Task<IActionResult> TransferChat([FromBody] TransferChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ChatSessionId) || request.TargetAgentId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid transfer request." });
            }

            var updated = await _assignmentService.TransferSessionAsync(request.ChatSessionId.Trim(), request.TargetAgentId, CurrentAgentName);
            if (updated == null)
            {
                return BadRequest(new { success = false, message = "Target agent not found or transfer failed." });
            }

            string groupName = $"session_{request.ChatSessionId.Trim()}";
            await _hubContext.Clients.Group(groupName).SendAsync("ChatTransferred", new
            {
                ChatSessionId = request.ChatSessionId.Trim(),
                NewAgentId = request.TargetAgentId,
                NewAgentName = updated.AgentName,
                TransferredBy = CurrentAgentName
            });

            await _hubContext.Clients.Group("agents_room").SendAsync("ChatTransferredNotice", new
            {
                ChatSessionId = request.ChatSessionId.Trim(),
                NewAgentId = request.TargetAgentId,
                NewAgentName = updated.AgentName
            });

            return Ok(new { success = true, session = updated });
        }

        [HttpGet]
        public async Task<IActionResult> GetNotes(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest();
            var notes = await _noteRepo.GetInternalNotesAsync(sessionId.Trim());
            return Ok(notes);
        }

        [HttpPost]
        public async Task<IActionResult> AddNote([FromBody] AddNoteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ChatSessionId) || string.IsNullOrWhiteSpace(request.NoteText))
            {
                return BadRequest(new { success = false, message = "Note text cannot be empty." });
            }

            var note = await _noteRepo.SaveInternalNoteAsync(request.ChatSessionId.Trim(), CurrentAgentId, request.NoteText.Trim());
            return Ok(new { success = true, note = note });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAvailability([FromBody] string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return BadRequest();

            await _agentRepo.UpdateAvailabilityAsync(CurrentAgentId, status.Trim());

            await _hubContext.Clients.Group("agents_room").SendAsync("AgentAvailabilityChanged", new
            {
                AgentId = CurrentAgentId,
                Status = status.Trim()
            });

            await _hubContext.Clients.Group("admin_monitoring_room").SendAsync("AgentAvailabilityChanged", new
            {
                AgentId = CurrentAgentId,
                Status = status.Trim()
            });

            return Ok(new { success = true, status = status.Trim() });
        }
    }
}
