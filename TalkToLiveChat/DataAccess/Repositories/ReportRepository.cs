using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TalkToLiveChat.Models.ViewModels;

namespace TalkToLiveChat.DataAccess.Repositories
{
    public interface IReportRepository
    {
        Task<List<DailyReportItem>> GetDailyReportAsync(DateTime? startDate, DateTime? endDate);
        Task<List<MonthlyReportItem>> GetMonthlyReportAsync(DateTime? startDate, DateTime? endDate);
        Task<List<ServiceReportItem>> GetServiceWiseReportAsync(DateTime? startDate, DateTime? endDate);
        Task<List<AgentReportItem>> GetAgentWiseReportAsync(DateTime? startDate, DateTime? endDate);
        Task<List<PendingReportItem>> GetPendingReportAsync();
        Task<List<CompletedReportItem>> GetCompletedReportAsync(DateTime? startDate, DateTime? endDate);
    }

    public class ReportRepository : IReportRepository
    {
        private readonly IDatabaseHelper _db;

        public ReportRepository(IDatabaseHelper db)
        {
            _db = db;
        }

        public async Task<List<DailyReportItem>> GetDailyReportAsync(DateTime? startDate, DateTime? endDate)
        {
            var p1 = new SqlParameter("@ReportType", SqlDbType.NVarChar, 30) { Value = "Daily" };
            var p2 = new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = (object?)startDate ?? DBNull.Value };
            var p3 = new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = (object?)endDate ?? DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetReports",
                CommandType.StoredProcedure,
                reader => new DailyReportItem
                {
                    ReportDate = reader.GetSafeDateTime("ReportDate"),
                    TotalChats = reader.GetSafeInt32("TotalChats"),
                    ActiveChats = reader.GetSafeInt32("ActiveChats"),
                    PendingChats = reader.GetSafeInt32("PendingChats"),
                    CompletedChats = reader.GetSafeInt32("CompletedChats"),
                    UniqueVisitors = reader.GetSafeInt32("UniqueVisitors")
                },
                p1, p2, p3
            );
        }

        public async Task<List<MonthlyReportItem>> GetMonthlyReportAsync(DateTime? startDate, DateTime? endDate)
        {
            var p1 = new SqlParameter("@ReportType", SqlDbType.NVarChar, 30) { Value = "Monthly" };
            var p2 = new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = (object?)startDate ?? DBNull.Value };
            var p3 = new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = (object?)endDate ?? DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetReports",
                CommandType.StoredProcedure,
                reader => new MonthlyReportItem
                {
                    ReportYear = reader.GetSafeInt32("ReportYear"),
                    ReportMonth = reader.GetSafeInt32("ReportMonth"),
                    MonthName = reader.GetSafeString("MonthName"),
                    TotalChats = reader.GetSafeInt32("TotalChats"),
                    ActiveChats = reader.GetSafeInt32("ActiveChats"),
                    PendingChats = reader.GetSafeInt32("PendingChats"),
                    CompletedChats = reader.GetSafeInt32("CompletedChats"),
                    UniqueVisitors = reader.GetSafeInt32("UniqueVisitors")
                },
                p1, p2, p3
            );
        }

        public async Task<List<ServiceReportItem>> GetServiceWiseReportAsync(DateTime? startDate, DateTime? endDate)
        {
            var p1 = new SqlParameter("@ReportType", SqlDbType.NVarChar, 30) { Value = "ServiceWise" };
            var p2 = new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = (object?)startDate ?? DBNull.Value };
            var p3 = new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = (object?)endDate ?? DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetReports",
                CommandType.StoredProcedure,
                reader => new ServiceReportItem
                {
                    ServiceId = reader.GetSafeInt32("ServiceId"),
                    ServiceName = reader.GetSafeString("ServiceName"),
                    TotalChats = reader.GetSafeInt32("TotalChats"),
                    ActiveChats = reader.GetSafeInt32("ActiveChats"),
                    PendingChats = reader.GetSafeInt32("PendingChats"),
                    CompletedChats = reader.GetSafeInt32("CompletedChats"),
                    AvgRating = reader.GetSafeNullableDouble("AvgRating")
                },
                p1, p2, p3
            );
        }

        public async Task<List<AgentReportItem>> GetAgentWiseReportAsync(DateTime? startDate, DateTime? endDate)
        {
            var p1 = new SqlParameter("@ReportType", SqlDbType.NVarChar, 30) { Value = "AgentWise" };
            var p2 = new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = (object?)startDate ?? DBNull.Value };
            var p3 = new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = (object?)endDate ?? DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetReports",
                CommandType.StoredProcedure,
                reader => new AgentReportItem
                {
                    AgentId = reader.GetSafeInt32("AgentId"),
                    AgentName = reader.GetSafeString("AgentName"),
                    Email = reader.GetSafeString("Email"),
                    AvailabilityStatus = reader.GetSafeString("AvailabilityStatus"),
                    TotalAssignedChats = reader.GetSafeInt32("TotalAssignedChats"),
                    ActiveChats = reader.GetSafeInt32("ActiveChats"),
                    CompletedChats = reader.GetSafeInt32("CompletedChats"),
                    AvgRating = reader.GetSafeNullableDouble("AvgRating")
                },
                p1, p2, p3
            );
        }

        public async Task<List<PendingReportItem>> GetPendingReportAsync()
        {
            var p1 = new SqlParameter("@ReportType", SqlDbType.NVarChar, 30) { Value = "Pending" };
            var p2 = new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = DBNull.Value };
            var p3 = new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetReports",
                CommandType.StoredProcedure,
                reader => new PendingReportItem
                {
                    ChatSessionId = reader.GetSafeString("ChatSessionId"),
                    MobileNumber = reader.GetSafeString("MobileNumber"),
                    ServiceName = reader.GetSafeString("ServiceName"),
                    StartDate = reader.GetSafeDateTime("StartDate"),
                    WaitingTimeMinutes = reader.GetSafeInt32("WaitingTimeMinutes")
                },
                p1, p2, p3
            );
        }

        public async Task<List<CompletedReportItem>> GetCompletedReportAsync(DateTime? startDate, DateTime? endDate)
        {
            var p1 = new SqlParameter("@ReportType", SqlDbType.NVarChar, 30) { Value = "Completed" };
            var p2 = new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = (object?)startDate ?? DBNull.Value };
            var p3 = new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = (object?)endDate ?? DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetReports",
                CommandType.StoredProcedure,
                reader => new CompletedReportItem
                {
                    ChatSessionId = reader.GetSafeString("ChatSessionId"),
                    MobileNumber = reader.GetSafeString("MobileNumber"),
                    ServiceName = reader.GetSafeString("ServiceName"),
                    AgentName = reader.GetSafeString("AgentName", "Unassigned"),
                    StartDate = reader.GetSafeDateTime("StartDate"),
                    EndDate = reader.GetSafeNullableDateTime("EndDate"),
                    DurationMinutes = reader.GetSafeNullableInt32("DurationMinutes"),
                    Rating = reader.GetSafeNullableInt32("Rating"),
                    Feedback = reader.GetSafeString("Feedback", "")
                },
                p1, p2, p3
            );
        }
    }
}
