using System;
using System.Threading.Tasks;
using TalkToLiveChat.DataAccess.Repositories;
using TalkToLiveChat.Models.Entities;
using TalkToLiveChat.Models.ViewModels;

namespace TalkToLiveChat.BusinessLogic
{
    public interface IChatAssignmentService
    {
        Task<StartChatResponse> InitiateChatSessionAsync(int visitorId, int serviceId);
        Task<ChatSession?> AutoAssignPendingSessionAsync(string sessionId);
        Task<ChatSession?> TransferSessionAsync(string sessionId, int targetAgentId, string transferredBy);
    }

    public class ChatAssignmentService : IChatAssignmentService
    {
        private readonly IAgentRepository _agentRepo;
        private readonly IChatSessionRepository _sessionRepo;
        private readonly IServiceRepository _serviceRepo;
        private readonly IMessageRepository _messageRepo;

        public ChatAssignmentService(
            IAgentRepository agentRepo,
            IChatSessionRepository sessionRepo,
            IServiceRepository serviceRepo,
            IMessageRepository messageRepo)
        {
            _agentRepo = agentRepo;
            _sessionRepo = sessionRepo;
            _serviceRepo = serviceRepo;
            _messageRepo = messageRepo;
        }

        public async Task<StartChatResponse> InitiateChatSessionAsync(int visitorId, int serviceId)
        {
            var service = await _serviceRepo.GetServiceByIdAsync(serviceId);
            string serviceName = service?.ServiceName ?? "Customer Support";

            // 1. Generate unique Chat Session ID (e.g. "CHAT-YYYYMMDD-XXXX")
            string sessionId = $"CHAT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";

            // 2. Automatically find an available agent assigned to the selected service
            var availableAgent = await _agentRepo.FindAvailableAgentForServiceAsync(serviceId);

            string initialStatus = (availableAgent != null) ? "Active" : "Pending";
            int? assignedAgentId = availableAgent?.AgentId;

            // 3. Create chat session in database
            var session = await _sessionRepo.CreateChatSessionAsync(sessionId, visitorId, serviceId, assignedAgentId, initialStatus);

            string welcomeMsg;
            if (availableAgent != null)
            {
                welcomeMsg = $"Hello! I am {availableAgent.AgentName} from {serviceName}. How may I help you today?";
                // Save agent welcome message to database
                await _messageRepo.SaveMessageAsync(
                    sessionId,
                    availableAgent.AgentId.ToString(),
                    "Agent",
                    welcomeMsg,
                    "Text"
                );
            }
            else
            {
                welcomeMsg = "Thank you for contacting us. Our support team will connect with you shortly.";
                // Save system pending notice to database
                await _messageRepo.SaveMessageAsync(
                    sessionId,
                    "System",
                    "System",
                    welcomeMsg,
                    "Text"
                );
            }

            return new StartChatResponse
            {
                Success = true,
                Message = "Chat session created successfully.",
                ChatSessionId = sessionId,
                SessionStatus = initialStatus,
                ServiceName = serviceName,
                AssignedAgentId = assignedAgentId,
                AgentName = availableAgent?.AgentName,
                AgentStatus = availableAgent?.AvailabilityStatus ?? "Offline",
                AgentProfileImage = availableAgent?.ProfileImage ?? "/images/default-avatar.png",
                WelcomeMessage = welcomeMsg
            };
        }

        public async Task<ChatSession?> AutoAssignPendingSessionAsync(string sessionId)
        {
            var session = await _sessionRepo.GetChatSessionByIdAsync(sessionId);
            if (session == null || session.SessionStatus != "Pending")
            {
                return session;
            }

            var agent = await _agentRepo.FindAvailableAgentForServiceAsync(session.ServiceId);
            if (agent != null)
            {
                var updated = await _sessionRepo.AssignAgentAsync(sessionId, agent.AgentId, "System Auto-Assign");
                string connectMsg = $"{agent.AgentName} has joined the conversation to assist you.";
                await _messageRepo.SaveMessageAsync(sessionId, "System", "System", connectMsg, "Text");
                return updated;
            }

            return session;
        }

        public async Task<ChatSession?> TransferSessionAsync(string sessionId, int targetAgentId, string transferredBy)
        {
            var targetAgent = await _agentRepo.GetAgentByIdAsync(targetAgentId);
            if (targetAgent == null) return null;

            var updatedSession = await _sessionRepo.AssignAgentAsync(sessionId, targetAgentId, transferredBy);

            // Save system message indicating transfer
            string notice = $"This conversation was transferred to {targetAgent.AgentName} by {transferredBy}.";
            await _messageRepo.SaveMessageAsync(sessionId, "System", "System", notice, "Text");

            return updatedSession;
        }
    }
}
