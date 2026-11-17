using System;
using System.Collections.Generic;
using System.Text;
using TalkToLiveChat.Models.ViewModels;

namespace TalkToLiveChat.BusinessLogic
{
    public interface IExportService
    {
        byte[] GenerateDailyReportCsv(List<DailyReportItem> data);
        byte[] GenerateMonthlyReportCsv(List<MonthlyReportItem> data);
        byte[] GenerateServiceReportCsv(List<ServiceReportItem> data);
        byte[] GenerateAgentReportCsv(List<AgentReportItem> data);
        byte[] GeneratePendingReportCsv(List<PendingReportItem> data);
        byte[] GenerateCompletedReportCsv(List<CompletedReportItem> data);
    }

    public class ExportService : IExportService
    {
        public byte[] GenerateDailyReportCsv(List<DailyReportItem> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Date,Total Chats,Active Chats,Pending Chats,Completed Chats,Unique Visitors");
            foreach (var item in data)
            {
                sb.AppendLine($"{item.ReportDate:yyyy-MM-dd},{item.TotalChats},{item.ActiveChats},{item.PendingChats},{item.CompletedChats},{item.UniqueVisitors}");
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public byte[] GenerateMonthlyReportCsv(List<MonthlyReportItem> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Year,Month,Month Name,Total Chats,Active Chats,Pending Chats,Completed Chats,Unique Visitors");
            foreach (var item in data)
            {
                sb.AppendLine($"{item.ReportYear},{item.ReportMonth},{EscapeCsv(item.MonthName)},{item.TotalChats},{item.ActiveChats},{item.PendingChats},{item.CompletedChats},{item.UniqueVisitors}");
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public byte[] GenerateServiceReportCsv(List<ServiceReportItem> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Service ID,Service Name,Total Chats,Active Chats,Pending Chats,Completed Chats,Average Rating");
            foreach (var item in data)
            {
                sb.AppendLine($"{item.ServiceId},{EscapeCsv(item.ServiceName)},{item.TotalChats},{item.ActiveChats},{item.PendingChats},{item.CompletedChats},{(item.AvgRating.HasValue ? item.AvgRating.Value.ToString("F1") : "N/A")}");
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public byte[] GenerateAgentReportCsv(List<AgentReportItem> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Agent ID,Agent Name,Email,Status,Total Assigned,Active Chats,Completed Chats,Average Rating");
            foreach (var item in data)
            {
                sb.AppendLine($"{item.AgentId},{EscapeCsv(item.AgentName)},{EscapeCsv(item.Email)},{item.AvailabilityStatus},{item.TotalAssignedChats},{item.ActiveChats},{item.CompletedChats},{(item.AvgRating.HasValue ? item.AvgRating.Value.ToString("F1") : "N/A")}");
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public byte[] GeneratePendingReportCsv(List<PendingReportItem> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Chat Session ID,Mobile Number,Service Name,Initiated At,Waiting Time (Mins)");
            foreach (var item in data)
            {
                sb.AppendLine($"{item.ChatSessionId},{item.MobileNumber},{EscapeCsv(item.ServiceName)},{item.StartDate:yyyy-MM-dd HH:mm:ss},{item.WaitingTimeMinutes}");
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        public byte[] GenerateCompletedReportCsv(List<CompletedReportItem> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Chat Session ID,Mobile Number,Service Name,Agent Name,Start Date,End Date,Duration (Mins),Rating,Feedback");
            foreach (var item in data)
            {
                sb.AppendLine($"{item.ChatSessionId},{item.MobileNumber},{EscapeCsv(item.ServiceName)},{EscapeCsv(item.AgentName ?? "")},{item.StartDate:yyyy-MM-dd HH:mm:ss},{(item.EndDate.HasValue ? item.EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "")},{item.DurationMinutes ?? 0},{item.Rating?.ToString() ?? "N/A"},{EscapeCsv(item.Feedback ?? "")}");
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }

    internal static class ByteArrayExtensions
    {
        public static byte[] Concat(this byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }
    }
}
