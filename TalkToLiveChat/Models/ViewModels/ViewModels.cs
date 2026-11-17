using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.Models.ViewModels
{
    public class MobileValidationRequest
    {
        [Required]
        public string CountryCode { get; set; } = "+91";

        [Required]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Please enter a valid 10-digit Indian mobile number starting with 6, 7, 8, or 9.")]
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class MobileValidationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int VisitorId { get; set; }
        public string CountryCode { get; set; } = "+91";
        public string MobileNumber { get; set; } = string.Empty;
    }

    public class StartChatRequest
    {
        [Required]
        public int VisitorId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid service.")]
        public int ServiceId { get; set; }
    }

    public class StartChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ChatSessionId { get; set; } = string.Empty;
        public string SessionStatus { get; set; } = "Pending"; // 'Pending' or 'Active'
        public string ServiceName { get; set; } = string.Empty;
        public int? AssignedAgentId { get; set; }
        public string? AgentName { get; set; }
        public string? AgentStatus { get; set; }
        public string? AgentProfileImage { get; set; }
        public string WelcomeMessage { get; set; } = string.Empty;
    }

    public class SendMessageDto
    {
        [Required]
        public string ChatSessionId { get; set; } = string.Empty;

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [Required]
        public string SenderType { get; set; } = "Customer"; // Customer, Agent, System

        public string? SenderName { get; set; }

        [Required]
        public string MessageText { get; set; } = string.Empty;

        public string MessageType { get; set; } = "Text";
    }

    public class AgentDashboardStats
    {
        public int TotalAssignedChats { get; set; }
        public int ActiveChats { get; set; }
        public int PendingChats { get; set; }
        public int CompletedChats { get; set; }
        public int UnreadMessages { get; set; }
    }

    public class AdminDashboardStats
    {
        public int TotalVisitors { get; set; }
        public int TotalChats { get; set; }
        public int ActiveChats { get; set; }
        public int PendingChats { get; set; }
        public int CompletedChats { get; set; }
        public int OnlineAgents { get; set; }
        public int OfflineAgents { get; set; }
        public List<ServiceWiseStats> ServiceStats { get; set; } = new List<ServiceWiseStats>();
    }

    public class ServiceWiseStats
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int TotalChats { get; set; }
        public int ActiveChats { get; set; }
        public int PendingChats { get; set; }
        public int CompletedChats { get; set; }
    }

    public class DailyReportItem
    {
        public DateTime ReportDate { get; set; }
        public int TotalChats { get; set; }
        public int ActiveChats { get; set; }
        public int PendingChats { get; set; }
        public int CompletedChats { get; set; }
        public int UniqueVisitors { get; set; }
    }

    public class MonthlyReportItem
    {
        public int ReportYear { get; set; }
        public int ReportMonth { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public int TotalChats { get; set; }
        public int ActiveChats { get; set; }
        public int PendingChats { get; set; }
        public int CompletedChats { get; set; }
        public int UniqueVisitors { get; set; }
    }

    public class ServiceReportItem
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int TotalChats { get; set; }
        public int ActiveChats { get; set; }
        public int PendingChats { get; set; }
        public int CompletedChats { get; set; }
        public double? AvgRating { get; set; }
    }

    public class AgentReportItem
    {
        public int AgentId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AvailabilityStatus { get; set; } = string.Empty;
        public int TotalAssignedChats { get; set; }
        public int ActiveChats { get; set; }
        public int CompletedChats { get; set; }
        public double? AvgRating { get; set; }
    }

    public class PendingReportItem
    {
        public string ChatSessionId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public int WaitingTimeMinutes { get; set; }
    }

    public class CompletedReportItem
    {
        public string ChatSessionId { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string? AgentName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DurationMinutes { get; set; }
        public int? Rating { get; set; }
        public string? Feedback { get; set; }
    }

    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class RatingSubmissionDto
    {
        [Required]
        public string ChatSessionId { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Feedback { get; set; }
    }

    public class TransferChatRequest
    {
        [Required]
        public string ChatSessionId { get; set; } = string.Empty;

        [Required]
        public int TargetAgentId { get; set; }
    }

    public class AddNoteRequest
    {
        [Required]
        public string ChatSessionId { get; set; } = string.Empty;

        [Required]
        public string NoteText { get; set; } = string.Empty;
    }
}
