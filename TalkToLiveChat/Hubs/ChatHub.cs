using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TalkToLiveChat.BusinessLogic;
using TalkToLiveChat.DataAccess.Repositories;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IMessageRepository _messageRepo;
        private readonly IChatSessionRepository _sessionRepo;
        private readonly IAgentRepository _agentRepo;
        private readonly IChatAssignmentService _assignmentService;

        public ChatHub(
            IMessageRepository messageRepo,
            IChatSessionRepository sessionRepo,
            IAgentRepository agentRepo,
            IChatAssignmentService assignmentService)
        {
            _messageRepo = messageRepo;
            _sessionRepo = sessionRepo;
            _agentRepo = agentRepo;
            _assignmentService = assignmentService;
        }

        public async Task JoinSession(string chatSessionId, string userType, string userId, string userName)
        {
            if (string.IsNullOrWhiteSpace(chatSessionId)) return;

            string groupName = $"session_{chatSessionId.Trim()}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            if (userType == "Agent")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "agents_room");
            }
            else if (userType == "Admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "agents_room");
                await Groups.AddToGroupAsync(Context.ConnectionId, "admin_monitoring_room");
            }

            // Notify session group
            await Clients.Group(groupName).SendAsync("UserJoined", new
            {
                ChatSessionId = chatSessionId,
                UserType = userType,
                UserId = userId,
                UserName = userName,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task LeaveSession(string chatSessionId)
        {
            if (string.IsNullOrWhiteSpace(chatSessionId)) return;
            string groupName = $"session_{chatSessionId.Trim()}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task SendMessage(string chatSessionId, string senderId, string senderName, string senderType, string messageText, string messageType = "Text", string? fileName = null, string? filePath = null, string? contentType = null)
        {
            if (string.IsNullOrWhiteSpace(chatSessionId) || string.IsNullOrWhiteSpace(messageText)) return;

            // 1. Save message to SQL Server via ADO.NET
            var savedMessage = await _messageRepo.SaveMessageAsync(chatSessionId, senderId, senderType, messageText, messageType);

            if (savedMessage != null && !string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(filePath))
            {
                int attachmentId = await _messageRepo.SaveAttachmentAsync(savedMessage.MessageId, fileName, filePath, contentType ?? "application/octet-stream");
                savedMessage.AttachmentId = attachmentId;
                savedMessage.FileName = fileName;
                savedMessage.FilePath = filePath;
                savedMessage.ContentType = contentType;
            }

            var broadcastPayload = new
            {
                MessageId = savedMessage?.MessageId ?? 0,
                ChatSessionId = chatSessionId,
                SenderId = senderId,
                SenderName = senderName,
                SenderType = senderType,
                MessageText = messageText,
                MessageType = messageType,
                SentDate = savedMessage?.SentDate ?? DateTime.UtcNow,
                IsRead = false,
                FileName = fileName,
                FilePath = filePath,
                ContentType = contentType
            };

            // 2. Broadcast to session group (Customer & assigned Agent)
            string groupName = $"session_{chatSessionId.Trim()}";
            await Clients.Group(groupName).SendAsync("ReceiveMessage", broadcastPayload);

            // 3. Broadcast to all agents and admin room for real-time notifications/sidebar badge updates
            await Clients.Group("agents_room").SendAsync("NewActivity", new
            {
                ChatSessionId = chatSessionId,
                SenderType = senderType,
                SenderName = senderName,
                MessagePreview = messageText.Length > 50 ? messageText.Substring(0, 47) + "..." : messageText,
                SentDate = DateTime.UtcNow
            });

            await Clients.Group("admin_monitoring_room").SendAsync("ReceiveMessage", broadcastPayload);
        }

        public async Task SendTyping(string chatSessionId, string senderName, string senderType, bool isTyping)
        {
            if (string.IsNullOrWhiteSpace(chatSessionId)) return;

            string groupName = $"session_{chatSessionId.Trim()}";
            await Clients.OthersInGroup(groupName).SendAsync("UserTyping", new
            {
                ChatSessionId = chatSessionId,
                SenderName = senderName,
                SenderType = senderType,
                IsTyping = isTyping
            });
        }

        public async Task MarkAsRead(string chatSessionId, string readerType)
        {
            if (string.IsNullOrWhiteSpace(chatSessionId)) return;

            await _messageRepo.MarkMessagesAsReadAsync(chatSessionId, readerType);

            string groupName = $"session_{chatSessionId.Trim()}";
            await Clients.Group(groupName).SendAsync("MessagesRead", new
            {
                ChatSessionId = chatSessionId,
                ReaderType = readerType,
                ReadDate = DateTime.UtcNow
            });
        }

        public async Task AcceptChat(string chatSessionId, int agentId, string agentName)
        {
            var updatedSession = await _sessionRepo.AssignAgentAsync(chatSessionId, agentId, agentName);

            string groupName = $"session_{chatSessionId.Trim()}";
            await Clients.Group(groupName).SendAsync("AgentAssigned", new
            {
                ChatSessionId = chatSessionId,
                AgentId = agentId,
                AgentName = agentName,
                SessionStatus = "Active"
            });

            // Notify agents and admin room
            await Clients.Group("agents_room").SendAsync("ChatStatusChanged", new
            {
                ChatSessionId = chatSessionId,
                AgentId = agentId,
                AgentName = agentName,
                Status = "Active"
            });
        }

        public async Task TransferChat(string chatSessionId, int targetAgentId, string transferredBy)
        {
            var updated = await _assignmentService.TransferSessionAsync(chatSessionId, targetAgentId, transferredBy);
            if (updated != null)
            {
                string groupName = $"session_{chatSessionId.Trim()}";
                await Clients.Group(groupName).SendAsync("ChatTransferred", new
                {
                    ChatSessionId = chatSessionId,
                    NewAgentId = targetAgentId,
                    NewAgentName = updated.AgentName,
                    TransferredBy = transferredBy
                });

                await Clients.Group("agents_room").SendAsync("ChatTransferredNotice", new
                {
                    ChatSessionId = chatSessionId,
                    NewAgentId = targetAgentId,
                    NewAgentName = updated.AgentName
                });
            }
        }

        public async Task CloseChat(string chatSessionId, string closedBy)
        {
            var session = await _sessionRepo.CloseChatSessionAsync(chatSessionId, closedBy);

            string groupName = $"session_{chatSessionId.Trim()}";
            await Clients.Group(groupName).SendAsync("ChatClosed", new
            {
                ChatSessionId = chatSessionId,
                ClosedBy = closedBy,
                ClosedDate = DateTime.UtcNow
            });

            await Clients.Group("agents_room").SendAsync("ChatClosedNotice", new
            {
                ChatSessionId = chatSessionId,
                ClosedBy = closedBy
            });
        }

        public async Task UpdateAvailability(int agentId, string status)
        {
            await _agentRepo.UpdateAvailabilityAsync(agentId, status);

            await Clients.Group("agents_room").SendAsync("AgentAvailabilityChanged", new
            {
                AgentId = agentId,
                Status = status
            });
            await Clients.Group("admin_monitoring_room").SendAsync("AgentAvailabilityChanged", new
            {
                AgentId = agentId,
                Status = status
            });
        }
    }
}
