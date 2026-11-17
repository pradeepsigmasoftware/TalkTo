using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TalkToLiveChat.BusinessLogic;
using TalkToLiveChat.DataAccess.Repositories;
using TalkToLiveChat.Hubs;
using TalkToLiveChat.Models.Entities;
using TalkToLiveChat.Models.ViewModels;

namespace TalkToLiveChat.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IAgentRepository _agentRepo;
        private readonly IServiceRepository _serviceRepo;
        private readonly IChatSessionRepository _sessionRepo;
        private readonly IVisitorRepository _visitorRepo;
        private readonly IMessageRepository _messageRepo;
        private readonly IReportRepository _reportRepo;
        private readonly IExportService _exportService;
        private readonly IAuthService _authService;
        private readonly IHubContext<ChatHub> _hubContext;

        public AdminController(
            IAgentRepository agentRepo,
            IServiceRepository serviceRepo,
            IChatSessionRepository sessionRepo,
            IVisitorRepository visitorRepo,
            IMessageRepository messageRepo,
            IReportRepository reportRepo,
            IExportService exportService,
            IAuthService authService,
            IHubContext<ChatHub> hubContext)
        {
            _agentRepo = agentRepo;
            _serviceRepo = serviceRepo;
            _sessionRepo = sessionRepo;
            _visitorRepo = visitorRepo;
            _messageRepo = messageRepo;
            _reportRepo = reportRepo;
            _exportService = exportService;
            _authService = authService;
            _hubContext = hubContext;
        }

        private string CurrentAdminName => User.FindFirstValue(ClaimTypes.Name) ?? "Administrator";

        // ==========================================
        // 1. DASHBOARD & LIVE MONITORING
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var stats = await _agentRepo.GetAdminDashboardStatsAsync();
            var services = await _serviceRepo.GetActiveServicesAsync();
            var agents = await _agentRepo.GetAllAgentsAsync();

            ViewBag.Stats = stats;
            ViewBag.Services = services;
            ViewBag.Agents = agents;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _agentRepo.GetAdminDashboardStatsAsync();
            return Ok(stats);
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions(int? serviceId, string? status, string? searchMobile, DateTime? startDate, DateTime? endDate)
        {
            var sessions = await _sessionRepo.GetChatSessionsListAsync(null, serviceId, status, searchMobile, startDate, endDate);
            return Ok(sessions);
        }

        [HttpGet]
        public async Task<IActionResult> GetSessionDetails(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest();

            var session = await _sessionRepo.GetChatSessionByIdAsync(sessionId.Trim());
            if (session == null) return NotFound();

            var messages = await _messageRepo.GetChatMessagesAsync(sessionId.Trim());
            return Ok(new { session = session, messages = messages });
        }

        [HttpPost]
        public async Task<IActionResult> AssignAgent([FromBody] TransferChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ChatSessionId) || request.TargetAgentId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid assignment request." });
            }

            var agent = await _agentRepo.GetAgentByIdAsync(request.TargetAgentId);
            if (agent == null) return BadRequest(new { success = false, message = "Agent not found." });

            var updated = await _sessionRepo.AssignAgentAsync(request.ChatSessionId.Trim(), request.TargetAgentId, $"Admin ({CurrentAdminName})");

            string notice = $"{agent.AgentName} was assigned to this conversation by Admin.";
            await _messageRepo.SaveMessageAsync(request.ChatSessionId.Trim(), "System", "System", notice, "Text");

            // SignalR notifications
            string groupName = $"session_{request.ChatSessionId.Trim()}";
            await _hubContext.Clients.Group(groupName).SendAsync("AgentAssigned", new
            {
                ChatSessionId = request.ChatSessionId.Trim(),
                AgentId = agent.AgentId,
                AgentName = agent.AgentName,
                SessionStatus = "Active"
            });

            await _hubContext.Clients.Group("agents_room").SendAsync("ChatStatusChanged", new
            {
                ChatSessionId = request.ChatSessionId.Trim(),
                AgentId = agent.AgentId,
                AgentName = agent.AgentName,
                Status = "Active"
            });

            return Ok(new { success = true, session = updated });
        }

        [HttpPost]
        public async Task<IActionResult> CloseSession([FromBody] string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest();

            var session = await _sessionRepo.CloseChatSessionAsync(sessionId.Trim(), $"Admin ({CurrentAdminName})");

            string groupName = $"session_{sessionId.Trim()}";
            await _hubContext.Clients.Group(groupName).SendAsync("ChatClosed", new
            {
                ChatSessionId = sessionId.Trim(),
                ClosedBy = $"Admin ({CurrentAdminName})",
                ClosedDate = DateTime.UtcNow
            });

            await _hubContext.Clients.Group("agents_room").SendAsync("ChatClosedNotice", new
            {
                ChatSessionId = sessionId.Trim(),
                ClosedBy = $"Admin ({CurrentAdminName})"
            });

            return Ok(new { success = true, session = session });
        }

        // ==========================================
        // 2. AGENT MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Agents()
        {
            var agents = await _agentRepo.GetAllAgentsAsync();
            var services = await _serviceRepo.GetActiveServicesAsync();

            ViewBag.Services = services;
            return View(agents);
        }

        [HttpGet]
        public async Task<IActionResult> GetAgent(int agentId)
        {
            var agent = await _agentRepo.GetAgentByIdAsync(agentId);
            if (agent == null) return NotFound();
            return Ok(agent);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAgent([FromForm] Agent agent, [FromForm] string? serviceIds, [FromForm] string? rawPassword)
        {
            if (string.IsNullOrWhiteSpace(agent.AgentName) || string.IsNullOrWhiteSpace(agent.Email))
            {
                return BadRequest(new { success = false, message = "Name and Email are required." });
            }

            if (!string.IsNullOrWhiteSpace(rawPassword))
            {
                agent.PasswordHash = _authService.HashPassword(rawPassword.Trim());
            }

            int savedId = await _agentRepo.SaveAgentAsync(agent);

            if (!string.IsNullOrEmpty(serviceIds))
            {
                var sIds = serviceIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(int.Parse)
                                     .ToList();
                await _agentRepo.SetAgentServicesAsync(savedId, sIds);
            }

            return Ok(new { success = true, agentId = savedId });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAgentStatus(int agentId, bool isActive)
        {
            var agent = await _agentRepo.GetAgentByIdAsync(agentId);
            if (agent == null) return NotFound();

            agent.IsActive = isActive;
            await _agentRepo.SaveAgentAsync(agent);

            return Ok(new { success = true });
        }

        // ==========================================
        // 3. SERVICE MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Services()
        {
            var services = await _serviceRepo.GetAllServicesAsync();
            return View(services);
        }

        [HttpPost]
        public async Task<IActionResult> SaveService([FromForm] ServiceItem service)
        {
            if (string.IsNullOrWhiteSpace(service.ServiceName))
            {
                return BadRequest(new { success = false, message = "Service name is required." });
            }

            int savedId = await _serviceRepo.SaveServiceAsync(service);
            return Ok(new { success = true, serviceId = savedId });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleServiceStatus(int serviceId, bool isActive)
        {
            await _serviceRepo.ToggleServiceStatusAsync(serviceId, isActive);
            return Ok(new { success = true });
        }

        // ==========================================
        // 4. VISITOR MANAGEMENT
        // ==========================================
        public async Task<IActionResult> Visitors(string? searchMobile)
        {
            var visitors = await _visitorRepo.GetVisitorsListAsync(searchMobile);
            ViewBag.SearchMobile = searchMobile;
            return View(visitors);
        }

        [HttpGet]
        public async Task<IActionResult> GetVisitorChats(int visitorId)
        {
            var chats = await _sessionRepo.GetChatSessionsListAsync(null, null, null, null, null, null);
            var filtered = chats.Where(c => c.VisitorId == visitorId).ToList();
            return Ok(filtered);
        }

        // ==========================================
        // 5. REPORTS & ANALYTICS
        // ==========================================
        public IActionResult Reports()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetReportData(string reportType, DateTime? startDate, DateTime? endDate)
        {
            switch (reportType)
            {
                case "Daily":
                    var daily = await _reportRepo.GetDailyReportAsync(startDate, endDate);
                    return Ok(daily);
                case "Monthly":
                    var monthly = await _reportRepo.GetMonthlyReportAsync(startDate, endDate);
                    return Ok(monthly);
                case "ServiceWise":
                    var serviceWise = await _reportRepo.GetServiceWiseReportAsync(startDate, endDate);
                    return Ok(serviceWise);
                case "AgentWise":
                    var agentWise = await _reportRepo.GetAgentWiseReportAsync(startDate, endDate);
                    return Ok(agentWise);
                case "Pending":
                    var pending = await _reportRepo.GetPendingReportAsync();
                    return Ok(pending);
                case "Completed":
                    var completed = await _reportRepo.GetCompletedReportAsync(startDate, endDate);
                    return Ok(completed);
                default:
                    return BadRequest(new { success = false, message = "Invalid report type." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportReport(string reportType, DateTime? startDate, DateTime? endDate)
        {
            byte[] bytes;
            string fileName;

            switch (reportType)
            {
                case "Daily":
                    var daily = await _reportRepo.GetDailyReportAsync(startDate, endDate);
                    bytes = _exportService.GenerateDailyReportCsv(daily);
                    fileName = $"Daily_Chat_Report_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;
                case "Monthly":
                    var monthly = await _reportRepo.GetMonthlyReportAsync(startDate, endDate);
                    bytes = _exportService.GenerateMonthlyReportCsv(monthly);
                    fileName = $"Monthly_Chat_Report_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;
                case "ServiceWise":
                    var serviceWise = await _reportRepo.GetServiceWiseReportAsync(startDate, endDate);
                    bytes = _exportService.GenerateServiceReportCsv(serviceWise);
                    fileName = $"Service_Wise_Report_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;
                case "AgentWise":
                    var agentWise = await _reportRepo.GetAgentWiseReportAsync(startDate, endDate);
                    bytes = _exportService.GenerateAgentReportCsv(agentWise);
                    fileName = $"Agent_Wise_Report_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;
                case "Pending":
                    var pending = await _reportRepo.GetPendingReportAsync();
                    bytes = _exportService.GeneratePendingReportCsv(pending);
                    fileName = $"Pending_Chat_Report_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;
                case "Completed":
                    var completed = await _reportRepo.GetCompletedReportAsync(startDate, endDate);
                    bytes = _exportService.GenerateCompletedReportCsv(completed);
                    fileName = $"Completed_Chat_Report_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;
                default:
                    return BadRequest("Invalid report type.");
            }

            return File(bytes, "text/csv", fileName);
        }
    }
}
