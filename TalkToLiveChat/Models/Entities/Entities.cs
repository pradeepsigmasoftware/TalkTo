using System;
using System.Collections.Generic;

namespace TalkToLiveChat.Models.Entities
{
    public class Visitor
    {
        public int VisitorId { get; set; }
        public string CountryCode { get; set; } = "+91";
        public string MobileNumber { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class ServiceItem
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public int AssignedAgentsCount { get; set; }
        public int TotalSessionsCount { get; set; }
    }

    public class Agent
    {
        public int AgentId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? ProfileImage { get; set; }
        public string AvailabilityStatus { get; set; } = "Offline"; // Online, Offline, Busy
        public int MaxConcurrentChats { get; set; } = 5;
        public string Role { get; set; } = "Agent"; // Admin, Agent
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int ActiveChatsCount { get; set; }
        public int TotalAssignedChats { get; set; }
        public string? AssignedServices { get; set; }
        public List<int> AssignedServiceIds { get; set; } = new List<int>();
    }

    public class ChatSession
    {
        public string ChatSessionId { get; set; } = string.Empty;
        public int VisitorId { get; set; }
        public string CountryCode { get; set; } = "+91";
        public string MobileNumber { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int? AssignedAgentId { get; set; }
        public string? AgentName { get; set; }
        public string? AgentEmail { get; set; }
        public string? AgentProfileImage { get; set; }
        public string? AgentStatus { get; set; }
        public string SessionStatus { get; set; } = "Pending"; // Pending, Active, Completed, Closed
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }
        public int TotalMessages { get; set; }
        public int UnreadCustomerMessages { get; set; }
        public string? LastMessageText { get; set; }
        public DateTime? LastMessageDate { get; set; }
        public int? Rating { get; set; }
        public string? Feedback { get; set; }
    }

    public class ChatMessage
    {
        public int MessageId { get; set; }
        public string ChatSessionId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderType { get; set; } = "Customer"; // Customer, Agent, System
        public string MessageText { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Text"; // Text, Image, File
        public DateTime SentDate { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
        public int? AttachmentId { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public string? ContentType { get; set; }
    }

    public class ChatAttachment
    {
        public int AttachmentId { get; set; }
        public int MessageId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    }

    public class ChatAssignment
    {
        public int AssignmentId { get; set; }
        public string ChatSessionId { get; set; } = string.Empty;
        public int AgentId { get; set; }
        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        public string AssignedBy { get; set; } = "System";
    }

    public class ChatInternalNote
    {
        public int NoteId { get; set; }
        public string ChatSessionId { get; set; } = string.Empty;
        public int AgentId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string NoteText { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class ChatRating
    {
        public int RatingId { get; set; }
        public string ChatSessionId { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Feedback { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class VisitorSummary
    {
        public int VisitorId { get; set; }
        public string CountryCode { get; set; } = "+91";
        public string MobileNumber { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? FirstContactDate { get; set; }
        public DateTime? LastContactDate { get; set; }
        public int TotalChats { get; set; }
        public string? ServicesRequested { get; set; }
    }
}
