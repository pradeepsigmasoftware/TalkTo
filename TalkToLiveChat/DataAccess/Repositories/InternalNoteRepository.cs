using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TalkToLiveChat.Models.Entities;

namespace TalkToLiveChat.DataAccess.Repositories
{
    public interface IInternalNoteRepository
    {
        Task<ChatInternalNote?> SaveInternalNoteAsync(string sessionId, int agentId, string noteText);
        Task<List<ChatInternalNote>> GetInternalNotesAsync(string sessionId);
    }

    public class InternalNoteRepository : IInternalNoteRepository
    {
        private readonly IDatabaseHelper _db;

        public InternalNoteRepository(IDatabaseHelper db)
        {
            _db = db;
        }

        public async Task<ChatInternalNote?> SaveInternalNoteAsync(string sessionId, int agentId, string noteText)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };
            var p2 = new SqlParameter("@AgentId", SqlDbType.Int) { Value = agentId };
            var p3 = new SqlParameter("@NoteText", SqlDbType.NVarChar, -1) { Value = noteText };

            return await _db.ExecuteReaderSingleAsync(
                "dbo.sp_SaveInternalNote",
                CommandType.StoredProcedure,
                reader => new ChatInternalNote
                {
                    NoteId = reader.GetSafeInt32("NoteId"),
                    ChatSessionId = reader.GetSafeString("ChatSessionId"),
                    AgentId = reader.GetSafeInt32("AgentId"),
                    AgentName = reader.GetSafeString("AgentName"),
                    NoteText = reader.GetSafeString("NoteText"),
                    CreatedDate = reader.GetSafeDateTime("CreatedDate")
                },
                p1, p2, p3
            );
        }

        public async Task<List<ChatInternalNote>> GetInternalNotesAsync(string sessionId)
        {
            var p1 = new SqlParameter("@ChatSessionId", SqlDbType.NVarChar, 50) { Value = sessionId };

            return await _db.ExecuteReaderListAsync(
                "dbo.sp_GetInternalNotes",
                CommandType.StoredProcedure,
                reader => new ChatInternalNote
                {
                    NoteId = reader.GetSafeInt32("NoteId"),
                    ChatSessionId = reader.GetSafeString("ChatSessionId"),
                    AgentId = reader.GetSafeInt32("AgentId"),
                    AgentName = reader.GetSafeString("AgentName"),
                    NoteText = reader.GetSafeString("NoteText"),
                    CreatedDate = reader.GetSafeDateTime("CreatedDate")
                },
                p1
            );
        }
    }
}
