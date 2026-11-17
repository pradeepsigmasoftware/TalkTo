using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.DataAccess.Repositories
{
    public interface IChatSessionRepository
    {
        Task<ChatSession?> CreateChatSessionAsync(string sessionId, int visitorId, int serviceId, int? agentId, string status);
        Task<ChatSession?> GetChatSessionByIdAsync(string sessionId);
        Task<ChatSession?> AssignAgentAsync(string sessionId, int agentId, string assignedBy);
        Task<ChatSession?> CloseChatSessionAsync(string sessionId, string closedBy);
        Task<List<ChatSession>> GetChatSessionsListAsync(int? agentId, int? serviceId, string? status, string? searchMobile, DateTime? startDate, DateTime? endDate);
        Task<int> SaveRatingAsync(string sessionId, int rating, string? feedback);
    }

    public class ChatSessionRepository : IChatSessionRepository
    {
        private readonly IDatabaseHelper _db;

        public ChatSessionRepository(IDatabaseHelper db)
        {
            _db = db;
        }

        public async Task<ChatSession?> CreateChatSessionAsync(string sessionId, int visitorId, int serviceId, int? agentId, string status)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };
            var p2 = new SqlParameter("@VisitorId", SqlDbType.Int) { Value = visitorId };
            var p3 = new SqlParameter("@ServiceId", SqlDbType.Int) { Value = serviceId };
            var p4 = new SqlParameter("@AssignedAgentId", SqlDbType.Int) { Value = (object?)agentId ?? DBNull.Value };
            var p5 = new SqlParameter("@SessionStatus", SqlDbType.NVarChar, 30) { Value = status };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_CreateChatSession",
                CommandType.StoredProcedure,
                MapChatSession,
                p1, p2, p3, p4, p5
            );
        }

        public async Task<ChatSession?> GetChatSessionByIdAsync(string sessionId)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_GetChatSessionById",
                CommandType.StoredProcedure,
                MapChatSession,
                p1
            );
        }

        public async Task<ChatSession?> AssignAgentAsync(string sessionId, int agentId, string assignedBy)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };
            var p2 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = agentId };
            var p3 = new SqlParameter("@AssignedBy", SqlDbType.NVarChar, 50) { Value = assignedBy };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_AssignAgentToSession",
                CommandType.StoredProcedure,
                MapChatSession,
                p1, p2, p3
            );
        }

        public async Task<ChatSession?> CloseChatSessionAsync(string sessionId, string closedBy)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };
            var p2 = new SqlParameter("@ClosedBy", SqlDbType.NVarChar, 50) { Value = closedBy };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_CloseChatSession",
                CommandType.StoredProcedure,
                MapChatSession,
                p1, p2
            );
        }

        public async Task<List<ChatSession>> GetChatSessionsListAsync(int? agentId, int? serviceId, string? status, string? searchMobile, DateTime? startDate, DateTime? endDate)
        {
            var p1 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = (object?)agentId ?? DBNull.Value };
            var p2 = new SqlParameter("@ServiceId", SqlDbType.Int) { Value = (object?)serviceId ?? DBNull.Value };
            var p3 = new SqlParameter("@SessionStatus", SqlDbType.NVarChar, 30) { Value = (object?)status ?? DBNull.Value };
            var p4 = new SqlParameter("@SearchMobile", SqlDbType.NVarChar, 50) { Value = (object?)searchMobile ?? DBNull.Value };
            var p5 = new SqlParameter("@StartDate", SqlDbType.DateTime2) { Value = (object?)startDate ?? DBNull.Value };
            var p6 = new SqlParameter("@EndDate", SqlDbType.DateTime2) { Value = (object?)endDate ?? DBNull.Value };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetChatSessionsList",
                CommandType.StoredProcedure,
                reader =>
                {
                    var session = MapChatSession(reader);
                    session.LastMessageText = reader.GetSafeString("LastMessageText", "");
                    session.LastMessageDate = reader.GetSafeNullableDateTime("LastMessageDate");
                    session.Rating = reader.GetSafeNullableInt32("Rating");
                    session.Feedback = reader.GetSafeString("Feedback", "");
                    return session;
                },
                p1, p2, p3, p4, p5, p6
            );
        }

        public async Task<int> SaveRatingAsync(string sessionId, int rating, string? feedback)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };
            var p2 = new SqlParameter("@Rating", SqlDbType.Int) { Value = rating };
            var p3 = new SqlParameter("@Feedback", SqlDbType.NVarChar, 1000) { Value = (object?)feedback ?? DBNull.Value };

            var result = await _db.ExecuteScalarAsync(
                "dbo.sp_SaveRating",
                CommandType.StoredProcedure,
                p1, p2, p3
            );

            return Convert.ToInt32(result);
        }

        private static ChatSession MapChatSession(SqlDataReader reader)
        {
            return new ChatSession
            {
                ChatSessionId = reader.GetSafeString("ChatSessionId"),
                VisitorId = reader.GetSafeInt32("VisitorId"),
                CountryCode = reader.GetSafeString("CountryCode"),
                MobileNumber = reader.GetSafeString("MobileNumber"),
                ServiceId = reader.GetSafeInt32("ServiceId"),
                ServiceName = reader.GetSafeString("ServiceName"),
                AssignedAgentId = reader.GetSafeNullableInt32("AssignedAgentId"),
                AgentName = reader.GetSafeString("AgentName", ""),
                AgentEmail = reader.GetSafeString("AgentEmail", ""),
                AgentProfileImage = reader.GetSafeString("AgentProfileImage", "/images/default-avatar.png"),
                AgentStatus = reader.GetSafeString("AgentStatus", "Offline"),
                SessionStatus = reader.GetSafeString("SessionStatus"),
                StartDate = reader.GetSafeDateTime("StartDate"),
                EndDate = reader.GetSafeNullableDateTime("EndDate"),
                TotalMessages = reader.GetSafeInt32("TotalMessages", 0),
                UnreadCustomerMessages = reader.GetSafeInt32("UnreadCustomerMessages", 0)
            };
        }
    }
}
