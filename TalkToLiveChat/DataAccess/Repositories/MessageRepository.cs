using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.DataAccess.Repositories
{
    public interface IMessageRepository
    {
        Task<ChatMessage?> SaveMessageAsync(string sessionId, string senderId, string senderType, string messageText, string messageType = "Text");
        Task<int> SaveAttachmentAsync(int messageId, string fileName, string filePath, string contentType);
        Task<List<ChatMessage>> GetChatMessagesAsync(string sessionId);
        Task MarkMessagesAsReadAsync(string sessionId, string readerType);
    }

    public class MessageRepository : IMessageRepository
    {
        private readonly IDatabaseHelper _db;

        public MessageRepository(IDatabaseHelper db)
        {
            _db = db;
        }

        public async Task<ChatMessage?> SaveMessageAsync(string sessionId, string senderId, string senderType, string messageText, string messageType = "Text")
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };
            var p2 = new SqlParameter("@SenderId", SqlDbType.NVarChar, 100) { Value = senderId };
            var p3 = new SqlParameter("@SenderType", SqlDbType.NVarChar, 20) { Value = senderType };
            var p4 = new SqlParameter("@MessageText", SqlDbType.NVarChar, -1) { Value = messageText };
            var p5 = new SqlParameter("@MessageType", SqlDbType.NVarChar, 20) { Value = messageType };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_SaveMessage",
                CommandType.StoredProcedure,
                MapChatMessage,
                p1, p2, p3, p4, p5
            );
        }

        public async Task<int> SaveAttachmentAsync(int messageId, string fileName, string filePath, string contentType)
        {
            var p1 = new SqlParameter("@MessageId", SqlDbType.Int) { Value = messageId };
            var p2 = new SqlParameter("@FileName", SqlDbType.NVarChar, 255) { Value = fileName };
            var p3 = new SqlParameter("@FilePath", SqlDbType.NVarChar, 500) { Value = filePath };
            var p4 = new SqlParameter("@ContentType", SqlDbType.NVarChar, 100) { Value = contentType };

            var result = await _db.ExecuteScalarAsync(
                "dbo.sp_SaveAttachment",
                CommandType.StoredProcedure,
                p1, p2, p3, p4
            );

            return Convert.ToInt32(result);
        }

        public async Task<List<ChatMessage>> GetChatMessagesAsync(string sessionId)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetChatMessages",
                CommandType.StoredProcedure,
                MapChatMessage,
                p1
            );
        }

        public async Task MarkMessagesAsReadAsync(string sessionId, string readerType)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };
            var p2 = new SqlParameter("@ReaderType", SqlDbType.NVarChar, 20) { Value = readerType };

            await _db.ExecuteNonQueryAsync(
                "dbo.sp_MarkMessagesAsRead",
                CommandType.StoredProcedure,
                p1, p2
            );
        }

        private static ChatMessage MapChatMessage(SqlDataReader reader)
        {
            return new ChatMessage
            {
                MessageId = reader.GetSafeInt32("MessageId"),
                ChatSessionId = reader.GetSafeString("ChatSessionId"),
                SenderId = reader.GetSafeString("SenderId"),
                SenderType = reader.GetSafeString("SenderType"),
                MessageText = reader.GetSafeString("MessageText"),
                MessageType = reader.GetSafeString("MessageType", "Text"),
                SentDate = reader.GetSafeDateTime("SentDate"),
                IsRead = reader.GetSafeBoolean("IsRead"),
                AttachmentId = reader.GetSafeNullableInt32("AttachmentId"),
                FileName = reader.GetSafeString("FileName", ""),
                FilePath = reader.GetSafeString("FilePath", ""),
                ContentType = reader.GetSafeString("ContentType", "")
            };
        }
    }
}
